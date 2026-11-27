using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Enums;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

/// <summary>清單與詳情所有登入者可用（內容依角色裁切）；維護限主管以上，標籤限員工。</summary>
public class AssetsController(
    IAssetService assets,
    IAssetExcelService excel,
    IAttachmentService attachments,
    IConfiguration configuration,
    ITimeProvider time) : Controller
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public IActionResult Index(AssetQuery query)
    {
        var result = assets.Search(query);
        query.Page = result.Page;
        return View(new AssetIndexViewModel(query, result, assets.GetFilterOptions()));
    }

    public IActionResult Details(Guid id)
    {
        ViewData["UploadRules"] = attachments.GetUploadRules();
        return View(assets.GetDetail(id));
    }

    /// <summary>QR 標籤的固定入口：以資產編號導向詳情頁，重置資料後標籤仍有效。</summary>
    [HttpGet("/Assets/ByNo/{assetNo}")]
    public IActionResult ByNo(string assetNo) =>
        RedirectToAction(nameof(Details), new { id = assets.GetIdByAssetNo(assetNo) });

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult Create()
    {
        var options = assets.GetFormOptions();
        return View("Form", new AssetFormViewModel { Options = options, SalvageValue = 0 });
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Create(AssetFormViewModel form)
    {
        form.Id = null;
        form.StatusLocked = false;
        var id = Guid.Empty;
        if (ModelState.IsValid && this.TryExecute(() => id = assets.Create(form.ToInput())))
        {
            TempData[Flash.Success] = "資產已新增。";
            return RedirectToAction(nameof(Details), new { id });
        }
        form.Options = assets.GetFormOptions();
        return View("Form", form);
    }

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult Edit(Guid id)
    {
        var model = AssetFormViewModel.From(assets.GetForEdit(id));
        model.Options = assets.GetFormOptions();
        return View("Form", model);
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Edit(Guid id, AssetFormViewModel form)
    {
        if (ModelState.IsValid && this.TryExecute(() => assets.Update(id, form.Version, form.ToInput())))
        {
            TempData[Flash.Success] = "資產資料已更新。";
            return RedirectToAction(nameof(Details), new { id });
        }
        var current = assets.GetForEdit(id);
        form.Id = id;
        form.AssetNo = current.AssetNo;
        form.StatusLocked = current.Input.Status == AssetStatus.UnderRepair;
        form.Options = assets.GetFormOptions();
        return View("Form", form);
    }

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult Transfer(Guid id)
    {
        var asset = assets.GetSummary(id);
        if (asset.Status == AssetStatus.Scrapped) return RejectScrapped(id, "已報廢資產不可轉移。");
        return View(new AssetTransferViewModel
        {
            Version = asset.Version, LocationId = asset.LocationId, DepartmentId = asset.DepartmentId,
            CustodianUserId = asset.CustodianUserId, Asset = asset, Options = assets.GetFormOptions()
        });
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Transfer(Guid id, AssetTransferViewModel model)
    {
        if (ModelState.IsValid && this.TryExecute(() => assets.Transfer(id, model.Version,
                new TransferInput(model.LocationId!.Value, model.DepartmentId, model.CustodianUserId, model.Reason))))
        {
            TempData[Flash.Success] = "資產已轉移並留下紀錄。";
            return RedirectToAction(nameof(Details), new { id });
        }
        model.Asset = assets.GetSummary(id);
        if (model.Asset.Status == AssetStatus.Scrapped) return RejectScrapped(id, "已報廢資產不可轉移。");
        model.Options = assets.GetFormOptions();
        return View(model);
    }

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult Scrap(Guid id)
    {
        var asset = assets.GetSummary(id);
        if (asset.Status == AssetStatus.Scrapped) return RejectScrapped(id, "此資產已報廢。");
        return View(new AssetScrapViewModel { Version = asset.Version, Asset = asset });
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Scrap(Guid id, AssetScrapViewModel model)
    {
        if (ModelState.IsValid && this.TryExecute(() => assets.Scrap(id, model.Version, model.Reason)))
        {
            TempData[Flash.Success] = "資產已報廢。";
            return RedirectToAction(nameof(Details), new { id });
        }
        model.Asset = assets.GetSummary(id);
        if (model.Asset.Status == AssetStatus.Scrapped) return RejectScrapped(id, "此資產已報廢。");
        return View(model);
    }

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult Export(AssetQuery query) =>
        File(excel.Export(query), XlsxContentType, $"資產清冊-{time.Today:yyyyMMdd}.xlsx");

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult ImportTemplate() =>
        File(excel.CreateImportTemplate(), XlsxContentType, "資產匯入範本.xlsx");

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult Import() => View(new AssetImportViewModel(null));

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Import(IFormFile? file)
    {
        ImportResult? result = null;
        if (file is null)
            ModelState.AddModelError(string.Empty, "請選擇 Excel 檔案。");
        else
            this.TryExecute(() => result = excel.Import(file.ToUpload(HttpContext)));
        return View(new AssetImportViewModel(result));
    }

    /// <summary>QR 標籤列印頁：指定 ids，或依清單篩選條件列印全部。</summary>
    [Authorize(Roles = Roles.Staff), HttpGet]
    public IActionResult Labels(AssetQuery query, [FromQuery] Guid[] ids, bool large = false)
    {
        // 手機掃描需要可從網路連到的網址；可用 App:PublicBaseUrl 指定。
        var configured = configuration["App:PublicBaseUrl"];
        var baseUrl = string.IsNullOrWhiteSpace(configured)
            ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}"
            : configured;
        var isLocal = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.IsLoopback;
        return View(new AssetLabelsViewModel(assets.GetLabels(ids, query, baseUrl), large, isLocal));
    }

    private IActionResult RejectScrapped(Guid id, string message)
    {
        TempData[Flash.Error] = message;
        return RedirectToAction(nameof(Details), new { id });
    }
}
