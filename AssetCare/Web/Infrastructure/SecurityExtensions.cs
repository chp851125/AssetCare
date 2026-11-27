using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AssetCare.Web.Infrastructure;

public static class SecurityExtensions
{
    public const string LoginRateLimit = "login";

    /// <summary>同一來源 IP 每分鐘的登入嘗試上限（帳號鎖定之外的第二道防線）。</summary>
    private const int LoginAttemptsPerMinute = 20;

    /// <summary>
    /// 腳本只允許本站檔案（頁面不可有行內腳本）；樣式因圖表、行事曆函式庫需要而允許行內。
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
        "font-src 'self' data:; media-src 'self' blob:; object-src 'none'; base-uri 'self'; form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IServiceCollection AddLoginRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimit, context =>
                // 只限制送出登入表單，開啟登入頁不計。
                HttpMethods.IsPost(context.Request.Method)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = LoginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1)
                        })
                    : RateLimitPartition.GetNoLimiter("read"));
        });

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                headers["Referrer-Policy"] = "same-origin";
                headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()";
                return Task.CompletedTask;
            });
            return next();
        });
}
