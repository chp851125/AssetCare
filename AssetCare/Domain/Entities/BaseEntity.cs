namespace AssetCare.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; }
    /// <summary>樂觀並行控制版本號，由 Repository 維護。</summary>
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
