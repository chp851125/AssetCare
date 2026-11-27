using AssetCare.Domain.Enums;

namespace AssetCare.Application.Dtos;

public class PlanQuery
{
    public string? Keyword { get; set; }
    public MaintenanceCycle? Cycle { get; set; }
    public MaintenanceDueState? State { get; set; }
    /// <summary>只看自己負責的計畫。</summary>
    public bool Mine { get; set; }
    public int Page { get; set; } = 1;
}

public record PlanListItem(Guid Id, string Name, string AssetNo, string AssetName, MaintenanceCycle Cycle,
    DateOnly? NextDueDate, decimal? NextDueUsage, decimal? CurrentUsage, string? UsageUnit,
    DateOnly? LastCompletedDate, string? AssigneeName, MaintenanceDueState State);

public record PlanOrderItem(Guid Id, string OrderNo, string CycleKey, WorkOrderStatus Status, string? AssigneeName,
    DateTime CreatedAt, DateTime? CompletedAt, bool Overdue);

public record PlanDetail(Guid Id, int Version, string Name, Guid AssetId, string AssetNo, string AssetName,
    int AssetVersion, MaintenanceCycle Cycle, Priority Priority, DateOnly? NextDueDate, int LeadDays,
    DateOnly? LastCompletedDate, decimal? UsageInterval, decimal? NextDueUsage, decimal? CurrentUsage,
    string? UsageUnit, string? AssigneeName, IReadOnlyList<string> ChecklistItems, bool IsActive,
    MaintenanceDueState State, IReadOnlyList<PlanOrderItem> Orders, bool CanManage, bool CanRecordUsage);

public record PlanInput(Guid AssetId, string? Name, MaintenanceCycle Cycle, Priority Priority, DateOnly? NextDueDate,
    int LeadDays, decimal? UsageInterval, decimal? NextDueUsage, Guid? AssigneeUserId, string? Checklist,
    bool IsActive);

public record PlanEditData(Guid Id, int Version, string AssetLabel, PlanInput Input);

public record PlanFormOptions(IReadOnlyList<Option> Assets, IReadOnlyList<Option> Technicians, int DefaultLeadDays);

/// <summary>行事曆事件；Kind 為 order（已產生工單）、planned（預定）或 overdue（逾期）。</summary>
public record CalendarEvent(string Title, string Start, string Url, string Kind);
