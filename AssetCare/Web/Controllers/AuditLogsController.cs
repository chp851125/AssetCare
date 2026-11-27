using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AuditLogsController(IAuditQueryService audits, ITimeProvider time) : Controller
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public IActionResult Index(AuditQuery query)
    {
        var search = audits.Search(query);
        query.Page = search.Result.Page;
        return View(new AuditIndexViewModel(query, search));
    }

    [HttpGet]
    public IActionResult Export(AuditQuery query) =>
        File(audits.Export(query), XlsxContentType, $"稽核紀錄_{time.ToLocal(time.UtcNow):yyyyMMdd-HHmm}.xlsx");
}
