using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[Authorize(Roles = Roles.Staff)]
public class MaintenancePlansController : Controller
{
    /// <summary>保養通知的連結目標；保養計畫功能於階段 4 實作。</summary>
    [HttpGet]
    public IActionResult Details(Guid id) =>
        View("ComingSoon", new ComingSoonViewModel("保養計畫", "bi-calendar-check", "預防保養功能將於階段 4 開放。"));
}
