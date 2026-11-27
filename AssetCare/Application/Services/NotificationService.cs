using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Exceptions;

namespace AssetCare.Application.Services;

public class NotificationService(
    IRepository<Notification> notifications,
    IBusinessLock businessLock,
    ICurrentUser currentUser,
    ITimeProvider time) : INotificationService
{
    private const int PageSize = 10;

    public int GetUnreadCount() =>
        currentUser.UserId is { } id ? notifications.Query(n => n.UserId == id && !n.IsRead).Count : 0;

    public PagedResult<NotificationItem> Search(NotificationQuery query)
    {
        var userId = currentUser.RequireUserId();
        var items = notifications.Query(n => n.UserId == userId)
            .Where(n => !query.UnreadOnly || !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationItem(n.Id, n.Type, n.Title, n.Message, n.IsRead, n.CreatedAt))
            .ToList();
        return PagedResult<NotificationItem>.From(items, query.Page, PageSize);
    }

    public string? Open(Guid id)
    {
        var userId = currentUser.RequireUserId();
        return businessLock.Run(() =>
        {
            // 他人的通知一律視為不存在。
            var notification = notifications.GetById(id) is { } n && n.UserId == userId
                ? n
                : throw new NotFoundException("找不到通知。");
            MarkRead(notification);
            return notification.LinkUrl;
        });
    }

    public void MarkAllRead()
    {
        var userId = currentUser.RequireUserId();
        businessLock.Run(() =>
        {
            foreach (var notification in notifications.Query(n => n.UserId == userId && !n.IsRead))
                MarkRead(notification);
        });
    }

    private void MarkRead(Notification notification)
    {
        if (notification.IsRead) return;
        notification.IsRead = true;
        notification.ReadAt = time.UtcNow;
        notifications.Update(notification);
    }
}
