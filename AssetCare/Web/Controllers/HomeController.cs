using AssetCare.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

public class HomeController(IDashboardService dashboard) : Controller
{
    public IActionResult Index() => View(dashboard.GetHomeSummary());
}
