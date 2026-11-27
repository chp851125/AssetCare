namespace AssetCare.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string IpAddress { get; set; } = "";
    /// <summary>動作代碼，見 AuditActions。</summary>
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid? EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? Detail { get; set; }
}
