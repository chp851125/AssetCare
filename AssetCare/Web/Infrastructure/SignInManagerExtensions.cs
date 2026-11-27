using System.Security.Claims;
using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AssetCare.Web.Infrastructure;

public static class AppClaimTypes
{
    public const string DisplayName = "assetcare:displayName";
    public const string SecurityStamp = "assetcare:securityStamp";
}

public static class SignInManagerExtensions
{
    public static Task SignInUserAsync(this HttpContext context, SignInUser user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(AppClaimTypes.DisplayName, user.DisplayName),
            new Claim(AppClaimTypes.SecurityStamp, user.SecurityStamp)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    /// <summary>帳號停用、刪除或安全戳記變更（例如重設密碼）後，讓舊 Cookie 失效。</summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = context.Principal?.FindFirstValue(AppClaimTypes.SecurityStamp);

        if (!Guid.TryParse(id, out var userId) || stamp is null || !auth.IsSessionValid(userId, stamp))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
