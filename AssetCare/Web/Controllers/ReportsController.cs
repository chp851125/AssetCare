using AssetCare.Application;
using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[Authorize(Roles = Roles.Managers)]
public class ReportsController(IReportService reports, ITimeProvider time) : Controller
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public IActionResult Index() => View(new ReportsViewModel(reports.GetDefaultRange()));

    [HttpGet]
    public IActionResult Export(ReportType type, DateOnly? from, DateOnly? to)
    {
        var defaults = reports.GetDefaultRange();
        // 日期格式錯誤時不可默默改用預設期間。
        if (!ModelState.IsValid)
        {
            TempData[Flash.Error] = "報表條件格式不正確。";
            return RedirectToAction(nameof(Index));
        }
        var content = reports.Export(type, new ReportRange(from ?? defaults.From, to ?? defaults.To));
        return File(content, XlsxContentType, $"{type.DisplayName()}_{time.ToLocal(time.UtcNow):yyyyMMdd-HHmm}.xlsx");
    }
}
