namespace AssetCare.Domain.Entities;

public class AssetTransfer : BaseEntity
{
    public Guid AssetId { get; set; }
    public Guid? FromLocationId { get; set; }
    public Guid? ToLocationId { get; set; }
    public Guid? FromDepartmentId { get; set; }
    public Guid? ToDepartmentId { get; set; }
    public Guid? FromCustodianUserId { get; set; }
    public Guid? ToCustodianUserId { get; set; }
    public string Reason { get; set; } = "";
    public Guid TransferredByUserId { get; set; }
    public DateTime TransferredAt { get; set; }
}
