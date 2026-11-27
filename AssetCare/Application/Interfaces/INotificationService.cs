using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>目前使用者自己的通知。</summary>
public interface INotificationService
{
    int GetUnreadCount();
    PagedResult<NotificationItem> Search(NotificationQuery query);
    /// <summary>標示已讀並回傳連結。只能操作自己的通知。</summary>
    string? Open(Guid id);
    void MarkAllRead();
}
