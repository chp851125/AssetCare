using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;

namespace AssetCare.Application.Services;

public class NotificationDispatcher(
    IRepository<Notification> notifications,
    IRepository<EmailLog> emails,
    IRepository<User> users,
    IEmailSender sender,
    ICurrentUser currentUser,
    ITimeProvider time,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public IReadOnlyList<Guid> ManagerIds() =>
        users.Query(u => u.IsActive && (u.Role == UserRole.Supervisor || u.Role == UserRole.Admin))
            .Select(u => u.Id).ToList();

    public void Send(IEnumerable<Guid?> userIds, NotificationType type, string title, string message, string linkUrl)
    {
        var targets = userIds.OfType<Guid>().Distinct().Where(id => id != currentUser.UserId).ToList();
        foreach (var id in targets)
        {
            // 通知是附帶動作：主要作業已完成，這裡失敗不可讓使用者以為作業失敗。
            try
            {
                if (users.GetById(id) is not { IsActive: true } user) continue;
                var notification = notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Type = type,
                    Title = title,
                    Message = message,
                    LinkUrl = linkUrl
                });
                SendEmail(user, notification);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "通知發送失敗：{Title} → {UserId}", title, id);
            }
        }
    }

    private void SendEmail(User user, Notification notification)
    {
        var log = new EmailLog
        {
            ToUserId = user.Id,
            ToEmail = user.Email,
            Subject = notification.Title,
            Body = notification.Message,
            NotificationId = notification.Id,
            SentAt = time.UtcNow,
            Status = EmailStatus.Sent
        };
        try
        {
            log.FileName = sender.Send(user.Email, user.DisplayName, log.Subject, log.Body, notification.LinkUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Email 寄送失敗：{To}", user.Email);
            log.Status = EmailStatus.Failed;
            log.ErrorMessage = ex.Message;
        }
        emails.Add(log);
    }
}
