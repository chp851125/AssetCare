using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

/// <summary>查詢與領用限員工；維護、入庫與盤點限主管以上。</summary>
[Authorize(Roles = Roles.Staff)]
public class PartsController(IPartService parts, IWorkOrderWorkflowService workflow) : Controller
{
    [HttpGet]
    public IActionResult Index(PartQuery query)
    {
        var result = parts.Search(query);
        query.Page = result.Page;
        return View(new PartIndexViewModel(query, result));
    }

    [HttpGet]
    public IActionResult Details(Guid id) => View(parts.GetDetail(id));

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult Create() => View("Form", Prepare(new PartFormViewModel()));

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Create(PartFormViewModel form)
    {
        form.Id = null;
        var id = Guid.Empty;
        if (ModelState.IsValid && this.TryExecute(() => id = parts.Create(form.ToInput())))
        {
            TempData[Flash.Success] = "零件已新增。";
            return RedirectToAction(nameof(Details), new { id });
        }
        return View("Form", Prepare(form));
    }

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult Edit(Guid id)
    {
        var data = parts.GetForEdit(id);
        var i = data.Input;
        return View("Form", Prepare(new PartFormViewModel
        {
            Id = data.Id, Version = data.Version, PartNo = data.PartNo, Name = i.Name, Spec = i.Spec, Unit = i.Unit,
            SafetyStock = i.SafetyStock, UnitCost = i.UnitCost, VendorId = i.VendorId,
            StorageLocation = i.StorageLocation, IsActive = i.IsActive
        }));
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Edit(Guid id, PartFormViewModel form)
    {
        if (ModelState.IsValid && this.TryExecute(() => parts.Update(id, form.Version, form.ToInput())))
        {
            TempData[Flash.Success] = "零件資料已更新。";
            return RedirectToAction(nameof(Details), new { id });
        }
        form.Id = id;
        form.PartNo = parts.GetForEdit(id).PartNo;
        return View("Form", Prepare(form));
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult StockIn(Guid id, int version, int? quantity, decimal? unitCost, string? note)
    {
        if (quantity is not { } count || unitCost is not { } cost) return Back(id, error: "請填寫入庫數量與單價。");
        parts.StockIn(id, version, count, cost, note);
        return Back(id, "入庫完成。");
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Adjust(Guid id, int version, int? actualQuantity, string? note)
    {
        if (actualQuantity is not { } actual) return Back(id, error: "請填寫盤點實際數量。");
        parts.Adjust(id, version, actual, note);
        return Back(id, "庫存已調整。");
    }

    /// <summary>
    /// 由零件頁領用到工單；workOrder 的值為「工單 Id|版本」。權限與庫存檢查同工單流程。
    /// </summary>
    [HttpPost]
    public IActionResult Issue(Guid id, string? workOrder, int? quantity)
    {
        var pair = workOrder?.Split('|');
        if (pair is not [var orderText, var versionText] || !Guid.TryParse(orderText, out var orderId) ||
            !int.TryParse(versionText, out var version) || quantity is not { } count)
            return Back(id, error: "請選擇工單並填寫領用數量。");
        workflow.IssuePart(orderId, version, new PartLine(id, count));
        return Back(id, "零件已領用。");
    }

    private IActionResult Back(Guid id, string? success = null, string? error = null)
    {
        if (success is not null) TempData[Flash.Success] = success;
        if (error is not null) TempData[Flash.Error] = error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private PartFormViewModel Prepare(PartFormViewModel model)
    {
        model.Vendors = parts.GetVendorOptions();
        return model;
    }
}
