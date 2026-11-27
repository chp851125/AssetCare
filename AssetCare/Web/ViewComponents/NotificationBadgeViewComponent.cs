using AssetCare.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.ViewComponents;

/// <summary>通知圖示上的未讀紅點。</summary>
public class NotificationBadgeViewComponent(INotificationService notifications) : ViewComponent
{
    public IViewComponentResult Invoke() => View(notifications.GetUnreadCount());
}
