using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Entities;

public class WorkOrder : BaseEntity
{
    public string OrderNo { get; set; } = "";
    public WorkOrderType Type { get; set; }
    public WorkOrderStatus Status { get; set; }
    public Priority Priority { get; set; }
    public Guid AssetId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>報修申請人；系統自動產生的保養工單為 null。</summary>
    public Guid? RequesterUserId { get; set; }
    public Guid? AssigneeUserId { get; set; }

    /// <summary>回應時限；保養工單為 null。</summary>
    public DateTime? ResponseDueAt { get; set; }
    public DateTime ResolveDueAt { get; set; }
    /// <summary>首次派工或接單時間。</summary>
    public DateTime? RespondedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    /// <summary>最近一次完工時間；驗收退回時清空。</summary>
    public DateTime? CompletedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public bool IsResponseOverdue { get; set; }
    public bool IsResolveOverdue { get; set; }
    public DateTime? ResponseOverdueNotifiedAt { get; set; }
    public DateTime? ResolveOverdueNotifiedAt { get; set; }

    public string? FaultCause { get; set; }
    public string? Resolution { get; set; }
    public decimal LaborHours { get; set; }
    public int ReworkCount { get; set; }

    public Guid? MaintenancePlanId { get; set; }
    /// <summary>保養週期識別（yyyy-MM-dd 或 usage:數值），同一計畫不可重複。</summary>
    public string? MaintenanceCycleKey { get; set; }
    public List<ChecklistItem> Checklist { get; set; } = [];

    public Guid? VendorId { get; set; }
    public DateOnly? SentOutDate { get; set; }
    public DateOnly? ExpectedReturnDate { get; set; }
    public DateOnly? ReturnedDate { get; set; }
    public decimal VendorCost { get; set; }
}

/// <summary>工單內嵌的保養檢查項目（非獨立實體）。</summary>
public class ChecklistItem
{
    public string Text { get; set; } = "";
    public bool IsChecked { get; set; }
    public string? Note { get; set; }
}
