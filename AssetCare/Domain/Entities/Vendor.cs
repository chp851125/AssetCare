namespace AssetCare.Domain.Entities;

public class Vendor : BaseEntity
{
    public string Name { get; set; } = "";
    public string? TaxId { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? ServiceScope { get; set; }
    public bool IsActive { get; set; } = true;
}
