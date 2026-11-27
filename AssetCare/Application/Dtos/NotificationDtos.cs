using AssetCare.Domain.Enums;

namespace AssetCare.Application.Dtos;

public class NotificationQuery
{
    public bool UnreadOnly { get; set; }
    public int Page { get; set; } = 1;
}

public record NotificationItem(Guid Id, NotificationType Type, string Title, string Message, bool IsRead,
    DateTime CreatedAt);
