using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

/// <summary>首頁儀表板，內容依角色決定；全局統計只提供主管以上（Service 亦檢查）。</summary>
public class HomeController(IDashboardService dashboard) : Controller
{
    public IActionResult Index(int? days) =>
        View(new HomeViewModel(dashboard.GetHomeSummary(),
            User.IsManager() ? dashboard.GetAnalytics(days) : null));
}
