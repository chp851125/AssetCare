using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AssetCare.Web.Infrastructure;

/// <summary>開發工具（快速切換使用者、工具列重置）的開放條件。</summary>
public class DevToolsAccess(IHostEnvironment environment, IConfiguration configuration)
{
    public bool IsDevelopment => environment.IsDevelopment();

    /// <summary>
    /// 免密碼切換身分僅限 Development，且預設只接受本機連線；
    /// 以手機測試需要時可將 DevTools:AllowRemoteQuickSwitch 設為 true。
    /// </summary>
    public bool CanQuickSwitch(HttpContext context)
    {
        if (!IsDevelopment) return false;
        if (configuration.GetValue<bool>("DevTools:AllowRemoteQuickSwitch")) return true;
        var remote = context.Connection.RemoteIpAddress;
        return remote is not null && IPAddress.IsLoopback(remote);
    }
}

/// <summary>非 Development 環境一律回 404。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DevelopmentOnlyAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (!context.HttpContext.RequestServices.GetRequiredService<DevToolsAccess>().IsDevelopment)
            context.Result = new NotFoundResult();
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
