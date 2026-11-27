using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

public class NotificationsController : Controller
{
    public IActionResult Index() =>
        View("ComingSoon", new ComingSoonViewModel("通知", "bi-bell", "通知中心將於階段 3 開放。"));
}
