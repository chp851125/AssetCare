using System.ComponentModel.DataAnnotations;
using AssetCare.Application.Dtos;
using AssetCare.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AssetCare.Web.ViewModels;

public record PlanIndexViewModel(PlanQuery Query, PagedResult<PlanListItem> Result);

public class PlanFormViewModel
{
    public Guid? Id { get; set; }
    public int Version { get; set; }

    [Display(Name = "資產"), Required(ErrorMessage = "請選擇資產")]
    public Guid? AssetId { get; set; }

    [Display(Name = "計畫名稱"), Required(ErrorMessage = "請輸入計畫名稱"), StringLength(100, ErrorMessage = "計畫名稱過長")]
    public string? Name { get; set; }

    [Display(Name = "保養週期")]
    public MaintenanceCycle Cycle { get; set; } = MaintenanceCycle.Monthly;

    [Display(Name = "工單優先級")]
    public Priority Priority { get; set; } = Priority.Normal;

    [Display(Name = "下次保養日")]
    public DateOnly? NextDueDate { get; set; }

    [Display(Name = "提前產生天數"), Range(0, 364, ErrorMessage = "提前產生天數須介於 0 到 364")]
    public int? LeadDays { get; set; }

    [Display(Name = "使用量間隔")]
    public decimal? UsageInterval { get; set; }

    [Display(Name = "下次保養使用量")]
    public decimal? NextDueUsage { get; set; }

    [Display(Name = "負責技術員")]
    public Guid? AssigneeUserId { get; set; }

    [Display(Name = "保養檢查清單"), Required(ErrorMessage = "請至少填寫一個檢查項目")]
    public string? Checklist { get; set; }

    [Display(Name = "啟用")]
    public bool IsActive { get; set; } = true;

    [ValidateNever]
    public PlanFormOptions Options { get; set; } = null!;

    /// <summary>編輯時顯示的資產名稱（資產不可更換）。</summary>
    public string? AssetLabel { get; set; }

    /// <summary>僅在 ModelState 驗證通過後呼叫。</summary>
    public PlanInput ToInput() => new(AssetId ?? Guid.Empty, Name, Cycle, Priority, NextDueDate, LeadDays ?? 0,
        UsageInterval, NextDueUsage, AssigneeUserId, Checklist, IsActive);
}

public class ChecklistEntryViewModel
{
    public bool IsChecked { get; set; }

    [StringLength(200, ErrorMessage = "檢查備註過長")]
    public string? Note { get; set; }

    public static IReadOnlyList<ChecklistEntry> ToEntries(IEnumerable<ChecklistEntryViewModel> items) =>
        items.Select(i => new ChecklistEntry(i.IsChecked, i.Note)).ToList();
}

/// <summary>檢查清單輸入欄位（_ChecklistInput）；Values 為送出失敗時保留的輸入。</summary>
public record ChecklistInputViewModel(IReadOnlyList<AssetCare.Domain.Entities.ChecklistItem> Items,
    IReadOnlyList<ChecklistEntryViewModel>? Values = null);
