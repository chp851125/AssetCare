using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[DevelopmentOnly]
public class DevController(IAuthService auth, DevToolsAccess devTools) : Controller
{
    /// <summary>免密碼切換身分；開放條件見 DevToolsAccess.CanQuickSwitch。</summary>
    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> SwitchUser(Guid userId)
    {
        if (!devTools.CanQuickSwitch(HttpContext)) return NotFound();

        var user = auth.GetSignInUser(userId);
        await HttpContext.SignInUserAsync(user);
        TempData[Flash.Success] = $"已切換為 {user.DisplayName}（{user.Role.Label()}）";
        // 一律回首頁，避免停留在新身分無權限的頁面。
        return RedirectToAction("Index", "Home");
    }
}
