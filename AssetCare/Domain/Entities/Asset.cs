using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Entities;

public class Asset : BaseEntity
{
    public string AssetNo { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid CategoryId { get; set; }
    public AssetStatus Status { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNo { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal SalvageValue { get; set; }
    public int UsefulLifeYears { get; set; }
    public DateOnly? WarrantyEndDate { get; set; }
    public Guid LocationId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? CustodianUserId { get; set; }
    public Guid? VendorId { get; set; }
    public string? Description { get; set; }
    /// <summary>累計使用量（依使用量保養用）。</summary>
    public decimal? CurrentUsage { get; set; }
    public string? UsageUnit { get; set; }
    public DateTime? ScrappedAt { get; set; }
    public string? ScrapReason { get; set; }
}
