using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using AssetCare.Application.Interfaces;
using AssetCare.Infrastructure;
using AssetCare.Infrastructure.Json.Seeding;
using AssetCare.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(context.HostingEnvironment.ContentRootPath, "logs", "assetcare-.log"),
        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<DevToolsAccess>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<BusinessExceptionFilter>();
    var messages = options.ModelBindingMessageProvider;
    messages.SetValueMustNotBeNullAccessor(_ => "此欄位為必填。");
    messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"「{value}」不是有效的值。");
    messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"「{value}」不是有效的值。");
    messages.SetValueIsInvalidAccessor(value => $"「{value}」不是有效的值。");
    messages.SetValueMustBeANumberAccessor(field => "請輸入數字。");
});

builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Clear();
    options.ViewLocationFormats.Add("/Web/Views/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Web/Views/Shared/{0}.cshtml");
});

// 讓中文直接輸出而非轉成 HTML 實體；仍會編碼 HTML 特殊字元。
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "AssetCare.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = SignInManagerExtensions.ValidatePrincipalAsync;
    });

builder.Services.AddAuthorizationBuilder()
    // 預設所有頁面都需登入，匿名頁面須明確標示 AllowAnonymous。
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddHealthChecks().AddCheck<DataDirectoryHealthCheck>("data-directory");

var app = builder.Build();

if (SeedCommands.IsSeedCommand(args))
    return SeedCommands.Run(args, app.Services, app.Environment);

app.Services.LoadAllRepositories();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
var culture = new CultureInfo("zh-TW");
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(culture.Name)
    .AddSupportedCultures(culture.Name)
    .AddSupportedUICultures(culture.Name));
app.UseSerilogRequestLogging();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
return 0;
