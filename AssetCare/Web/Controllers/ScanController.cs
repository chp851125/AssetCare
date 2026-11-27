using AssetCare.Application.Interfaces;
using AssetCare.Domain.Exceptions;
using AssetCare.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

public class ScanController(IAssetService assets) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    /// <summary>掃描或手動輸入資產編號後導向資產頁。</summary>
    [HttpGet]
    public IActionResult Asset(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return Retry("請輸入資產編號。");
        try
        {
            return RedirectToAction("Details", "Assets", new { id = assets.GetIdByAssetNo(code) });
        }
        catch (NotFoundException)
        {
            return Retry($"找不到資產編號「{code.Trim()}」。");
        }
    }

    private IActionResult Retry(string message)
    {
        TempData[Flash.Error] = message;
        return RedirectToAction(nameof(Index));
    }
}
