namespace AssetCare.Application.Dtos;

public class VendorQuery
{
    public string? Keyword { get; set; }
    public bool IncludeInactive { get; set; }
    public int Page { get; set; } = 1;
}

public record VendorListItem(Guid Id, string Name, string? TaxId, string? ContactName, string? Phone, string? Email,
    string? ServiceScope, bool IsActive, int OutsourcedCount);

public record VendorInput(string? Name, string? TaxId, string? ContactName, string? Phone, string? Email,
    string? Address, string? ServiceScope, bool IsActive);

public record VendorEditData(Guid Id, int Version, VendorInput Input);
