namespace AssetCare.Domain.Entities;

public class WorkOrderPart : BaseEntity
{
    public Guid WorkOrderId { get; set; }
    public Guid PartId { get; set; }
    public int Quantity { get; set; }
    /// <summary>領用當下單價。</summary>
    public decimal UnitCost { get; set; }
    public Guid IssuedByUserId { get; set; }
    public DateTime IssuedAt { get; set; }
}
