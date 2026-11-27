using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

/// <summary>
/// 清單、報修、檢視、留言、驗收與取消開放給登入者（資料擁有權由 Service 檢查）；
/// 派工限主管以上，接單限技術員，其餘處理動作限員工（技術員僅限指派給自己的工單）。
/// </summary>
public class WorkOrdersController(
    IWorkOrderService orders,
    IWorkOrderWorkflowService workflow,
    IAttachmentService attachments) : Controller
{
    /// <summary>照片上傳的要求大小上限（5 張 × 10 MB 加上表單欄位）。</summary>
    private const int UploadLimit = 55 * 1024 * 1024;

    [HttpGet]
    public IActionResult Index(WorkOrderQuery query)
    {
        var list = orders.Search(query);
        query.Scope = list.Scope;
        query.Page = list.Result.Page;
        return View(new WorkOrderIndexViewModel(query, list));
    }

    /// <summary>底部導覽「我的工單」：依角色開啟預設分頁。</summary>
    [HttpGet]
    public IActionResult Mine() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Create(Guid? assetId)
    {
        if (assetId is not { } id) return View("PickAsset", new ReportPickViewModel(orders.GetReportableAssets()));
        try
        {
            return View(Prepare(new ReportViewModel { AssetId = id }, id));
        }
        catch (BusinessRuleException ex)
        {
            // 掃到已報廢資產的標籤時，改顯示資產資料並說明原因。
            TempData[Flash.Error] = ex.Message;
            return RedirectToAction("Details", "Assets", new { id });
        }
    }

    [HttpPost, RequestSizeLimit(UploadLimit)]
    public IActionResult Create(ReportViewModel form)
    {
        var id = Guid.Empty;
        if (ModelState.IsValid && this.TryExecute(() => id = workflow.Report(
                new ReportInput(form.AssetId!.Value, form.Description, form.Priority), Uploads(form.Photos))))
        {
            TempData[Flash.Success] = "報修已送出，我們會盡快處理。";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (form.AssetId is not { } assetId) return RedirectToAction(nameof(Create));
        return View(Prepare(form, assetId));
    }

    [HttpGet]
    public IActionResult Details(Guid id) =>
        View(new WorkOrderDetailViewModel(orders.GetDetail(id), attachments.GetPhotoRules()));

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Assign(Guid id, int version, Guid? technicianId, string? note)
    {
        if (technicianId is not { } technician) return Back(id, error: "請選擇技術員。");
        workflow.Assign(id, version, technician, note);
        return Back(id, "派工完成。");
    }

    [Authorize(Roles = nameof(UserRole.Technician)), HttpPost]
    public IActionResult Accept(Guid id, int version)
    {
        workflow.Accept(id, version);
        return Back(id, "已接單。");
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public IActionResult Start(Guid id, int version)
    {
        workflow.Start(id, version);
        return Back(id, "已開始處理。");
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public IActionResult Wait(Guid id, int version, string? reason)
    {
        workflow.WaitForParts(id, version, reason);
        return Back(id, "已設為待料。");
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public IActionResult Resume(Guid id, int version, string? note)
    {
        workflow.Resume(id, version, note);
        return Back(id, "已恢復處理。");
    }

    [Authorize(Roles = Roles.Staff), HttpGet]
    public IActionResult Complete(Guid id)
    {
        var context = orders.GetCompleteContext(id);
        return View(Prepare(new CompleteViewModel
        {
            Version = context.Version,
            FaultCause = context.FaultCause,
            Resolution = context.Resolution,
            LaborHours = context.LaborHours > 0 ? context.LaborHours : null
        }, context));
    }

    [Authorize(Roles = Roles.Staff), HttpPost, RequestSizeLimit(UploadLimit)]
    public IActionResult Complete(Guid id, CompleteViewModel form)
    {
        var lines = new List<PartLine>();
        foreach (var row in form.Parts.Where(p => p.PartId is not null || p.Quantity is not null))
        {
            if (row.PartId is { } part && row.Quantity is { } quantity) lines.Add(new PartLine(part, quantity));
            else ModelState.AddModelError(string.Empty, "使用零件需同時選擇零件並填寫數量。");
        }
        if (ModelState.IsValid && this.TryExecute(() => workflow.Complete(id, form.Version,
                new CompleteInput(form.FaultCause, form.Resolution, form.LaborHours!.Value, lines,
                    Uploads(form.Photos)))))
        {
            return Back(id, "完工回報已送出，等待驗收。");
        }
        return View(Prepare(form, orders.GetCompleteContext(id)));
    }

    [HttpPost]
    public IActionResult Verify(Guid id, int version, string? note)
    {
        workflow.Verify(id, version, note);
        return Back(id, "驗收通過，工單已結案。");
    }

    [HttpPost]
    public IActionResult Reject(Guid id, int version, string? reason)
    {
        workflow.Reject(id, version, reason);
        return Back(id, "已退回技術員重新處理。");
    }

    [HttpPost]
    public IActionResult Cancel(Guid id, int version, string? reason)
    {
        workflow.Cancel(id, version, reason);
        return Back(id, "工單已取消。");
    }

    [HttpPost]
    public IActionResult Comment(Guid id, string? comment)
    {
        workflow.AddComment(id, comment);
        return Back(id, "留言已送出。", "timeline");
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public IActionResult IssuePart(Guid id, int version, Guid? partId, int? quantity)
    {
        if (partId is not { } part || quantity is not { } count) return Back(id, error: "請選擇零件並填寫數量。");
        workflow.IssuePart(id, version, new PartLine(part, count));
        return Back(id, "零件已領用。", "parts");
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public IActionResult SendToVendor(Guid id, int version, Guid? vendorId, DateOnly? sentOutDate,
        DateOnly? expectedReturnDate, string? note)
    {
        if (vendorId is not { } vendor || sentOutDate is not { } sent || expectedReturnDate is not { } expected)
            return Back(id, error: "請選擇廠商並填寫外送日期與預計回廠日。");
        workflow.SendToVendor(id, version, new SendOutInput(vendor, sent, expected, note));
        return Back(id, "已登錄外送維修。", "vendor");
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public IActionResult ReturnFromVendor(Guid id, int version, DateOnly? returnedDate, decimal? cost, string? note)
    {
        if (returnedDate is not { } date || cost is not { } amount)
            return Back(id, error: "請填寫回廠日期與維修費用（無費用請填 0）。");
        workflow.ReturnFromVendor(id, version, new VendorReturnInput(date, amount, note));
        return Back(id, "已登錄廠商送回。", "vendor");
    }

    [HttpPost, RequestSizeLimit(UploadLimit)]
    public IActionResult UploadPhotos(Guid id, List<IFormFile> photos, string? caption)
    {
        attachments.UploadForWorkOrder(id, Uploads(photos), caption);
        return Back(id, "照片已上傳。", "photos");
    }

    private List<UploadedFile> Uploads(IEnumerable<IFormFile> files) =>
        files.Select(f => f.ToUpload(HttpContext)).ToList();

    private IActionResult Back(Guid id, string? success = null, string? fragment = null, string? error = null)
    {
        if (success is not null) TempData[Flash.Success] = success;
        if (error is not null) TempData[Flash.Error] = error;
        return RedirectToAction(nameof(Details), "WorkOrders", new { id }, fragment);
    }

    private ReportViewModel Prepare(ReportViewModel model, Guid assetId)
    {
        model.Context = orders.GetReportContext(assetId);
        model.Rules = attachments.GetPhotoRules();
        return model;
    }

    private CompleteViewModel Prepare(CompleteViewModel model, CompleteContext context)
    {
        model.Context = context;
        model.Rules = attachments.GetPhotoRules();
        while (model.Parts.Count < CompleteViewModel.PartRows) model.Parts.Add(new PartLineViewModel());
        return model;
    }
}
