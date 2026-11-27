using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    /// <summary>站內相對路徑，例如 /WorkOrders/Details/{id}。</summary>
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
