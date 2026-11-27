using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

public class ScanController : Controller
{
    public IActionResult Index() =>
        View("ComingSoon", new ComingSoonViewModel("掃碼", "bi-qr-code-scan", "掃描設備 QR Code 報修的功能將於階段 3 開放。"));
}
