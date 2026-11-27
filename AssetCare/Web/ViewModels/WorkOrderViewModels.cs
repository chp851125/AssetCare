using System.ComponentModel.DataAnnotations;
using AssetCare.Application.Dtos;
using AssetCare.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AssetCare.Web.ViewModels;

public record WorkOrderIndexViewModel(WorkOrderQuery Query, WorkOrderList List);

public class ReportViewModel
{
    [Display(Name = "報修資產"), Required(ErrorMessage = "請選擇要報修的資產")]
    public Guid? AssetId { get; set; }

    [Display(Name = "故障描述"), Required(ErrorMessage = "請描述故障情形"), StringLength(1000, ErrorMessage = "故障描述過長")]
    public string? Description { get; set; }

    [Display(Name = "優先級")]
    public Priority Priority { get; set; } = Priority.Normal;

    [Display(Name = "照片")]
    public List<IFormFile> Photos { get; set; } = [];

    [ValidateNever]
    public ReportContext Context { get; set; } = null!;

    [ValidateNever]
    public UploadRules Rules { get; set; } = null!;
}

public record ReportPickViewModel(IReadOnlyList<Option> Assets);

public class PartLineViewModel
{
    public Guid? PartId { get; set; }

    [Range(1, 1_000_000, ErrorMessage = "領用數量必須大於 0")]
    public int? Quantity { get; set; }
}

public class CompleteViewModel
{
    /// <summary>完工表單一次可填的零件列數。</summary>
    public const int PartRows = 3;

    public int Version { get; set; }

    [Display(Name = "故障原因"), Required(ErrorMessage = "請輸入故障原因"), StringLength(1000, ErrorMessage = "故障原因過長")]
    public string? FaultCause { get; set; }

    [Display(Name = "處理方式"), Required(ErrorMessage = "請輸入處理方式"), StringLength(1000, ErrorMessage = "處理方式過長")]
    public string? Resolution { get; set; }

    [Display(Name = "工時（小時）"), Required(ErrorMessage = "請輸入工時"), Range(0, 1000, ErrorMessage = "工時須介於 0 到 1000 小時")]
    public decimal? LaborHours { get; set; }

    public List<PartLineViewModel> Parts { get; set; } = [];

    /// <summary>保養工單的檢查清單；一般工單為空。</summary>
    public List<ChecklistEntryViewModel> Checklist { get; set; } = [];

    [Display(Name = "完工照片")]
    public List<IFormFile> Photos { get; set; } = [];

    [ValidateNever]
    public CompleteContext Context { get; set; } = null!;

    [ValidateNever]
    public UploadRules Rules { get; set; } = null!;
}

public record WorkOrderDetailViewModel(WorkOrderDetail Order, UploadRules Rules);

/// <summary>照片選擇欄位（_PhotoInput）。</summary>
public record PhotoInputViewModel(string Name, UploadRules Rules);
