namespace AssetCare.Domain.Entities;

public class AssetCategory : BaseEntity
{
    /// <summary>用於資產編號 AST-類別代碼-0001。</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int DefaultUsefulLifeYears { get; set; }
    public bool IsActive { get; set; } = true;
}
