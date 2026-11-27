using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController(IDataResetService dataReset) : Controller
{
    public IActionResult Index() => View();

    [DevelopmentOnly, HttpPost]
    public async Task<IActionResult> ResetData()
    {
        dataReset.ResetToSampleData();
        await HttpContext.SignOutAsync();
        TempData[Flash.Success] = "已重置為範例資料，請重新登入。";
        return RedirectToAction("Login", "Account");
    }
}
