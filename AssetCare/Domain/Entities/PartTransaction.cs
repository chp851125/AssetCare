using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Entities;

public class PartTransaction : BaseEntity
{
    public Guid PartId { get; set; }
    public PartTransactionType Type { get; set; }
    /// <summary>庫存增減量：入庫為正、領用為負。</summary>
    public int Quantity { get; set; }
    public int BalanceAfter { get; set; }
    public decimal UnitCost { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid OperatorUserId { get; set; }
    public string? Note { get; set; }
    public DateTime OccurredAt { get; set; }
}
