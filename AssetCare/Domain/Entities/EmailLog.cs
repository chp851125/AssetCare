using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Entities;

public class EmailLog : BaseEntity
{
    public Guid? ToUserId { get; set; }
    public string ToEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>outbox 資料夾中的 .eml 檔名。</summary>
    public string FileName { get; set; } = "";
    public EmailStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; }
    public Guid? NotificationId { get; set; }
}
