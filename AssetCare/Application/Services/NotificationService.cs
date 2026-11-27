using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;

namespace AssetCare.Application.Services;

public class NotificationService(IRepository<Notification> notifications, ICurrentUser currentUser) : INotificationService
{
    public int GetUnreadCount() =>
        currentUser.UserId is { } id ? notifications.Query(n => n.UserId == id && !n.IsRead).Count : 0;
}
