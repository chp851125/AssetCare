using AssetCare.Application.Interfaces;
using AssetCare.Domain.Enums;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.ViewComponents;

/// <summary>右上角開發工具列：快速切換使用者與重置範例資料。</summary>
public class DevToolbarViewComponent(DevToolsAccess devTools, IUserService users, ICurrentUser currentUser) : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        if (!devTools.IsDevelopment) return Content(string.Empty);
        var options = devTools.CanQuickSwitch(HttpContext) ? users.GetActiveUserOptions() : [];
        return View(new DevToolbarViewModel(options, currentUser.Role == UserRole.Admin));
    }
}
