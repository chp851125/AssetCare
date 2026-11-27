using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

public class WorkOrdersController : Controller
{
    public IActionResult Mine() =>
        View("ComingSoon", new ComingSoonViewModel("我的工單", "bi-clipboard-check", "報修與工單功能將於階段 3 開放。"));
}
