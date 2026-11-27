namespace AssetCare.Domain.Entities;

public class Location : BaseEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>上層地點（廠區 → 樓層 → 區域）。</summary>
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
