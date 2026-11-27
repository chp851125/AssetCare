using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

/// <summary>每位使用者只能看到與操作自己的通知。</summary>
public class NotificationsController(INotificationService notifications) : Controller
{
    [HttpGet]
    public IActionResult Index(NotificationQuery query)
    {
        var result = notifications.Search(query);
        query.Page = result.Page;
        return View(new NotificationIndexViewModel(query, result, notifications.GetUnreadCount()));
    }

    [HttpPost]
    public IActionResult Open(Guid id)
    {
        var link = notifications.Open(id);
        // 只導向站內網址。
        return Url.IsLocalUrl(link) ? LocalRedirect(Url.Content("~" + link)) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult MarkAllRead()
    {
        notifications.MarkAllRead();
        TempData[Flash.Success] = "已全部標示為已讀。";
        return RedirectToAction(nameof(Index));
    }
}
