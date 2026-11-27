using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Entities;

public class WorkOrderLog : BaseEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderAction Action { get; set; }
    /// <summary>狀態轉移前後；不涉及狀態變更（如留言）時兩者皆為 null。建立時 From 為 null。</summary>
    public WorkOrderStatus? FromStatus { get; set; }
    public WorkOrderStatus? ToStatus { get; set; }
    public string? Comment { get; set; }
    /// <summary>系統動作為 null。</summary>
    public Guid? ActorUserId { get; set; }
    public DateTime OccurredAt { get; set; }
}
