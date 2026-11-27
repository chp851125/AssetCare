namespace AssetCare.Domain.Entities;

public class Part : BaseEntity
{
    public string PartNo { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Spec { get; set; }
    public string Unit { get; set; } = "";
    public int StockQuantity { get; set; }
    public int SafetyStock { get; set; }
    public decimal UnitCost { get; set; }
    public Guid? VendorId { get; set; }
    public string? StorageLocation { get; set; }
    public bool IsActive { get; set; } = true;
}
