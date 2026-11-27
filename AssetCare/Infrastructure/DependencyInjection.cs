using AssetCare.Application.Interfaces;
using AssetCare.Application.Services;
using AssetCare.Domain.Entities;
using AssetCare.Infrastructure.Background;
using AssetCare.Infrastructure.Email;
using AssetCare.Infrastructure.Excel;
using AssetCare.Infrastructure.Files;
using AssetCare.Infrastructure.Json;
using AssetCare.Infrastructure.Qr;
using Microsoft.AspNetCore.Identity;

namespace AssetCare.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JsonDataOptions>(configuration.GetSection("JsonData"));
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<JsonDataStore>();
        services.AddSingleton<IDataStore>(sp => sp.GetRequiredService<JsonDataStore>());
        services.AddSingleton(typeof(IRepository<>), typeof(JsonFileRepository<>));
        services.AddSingleton<ISpreadsheetService, ClosedXmlSpreadsheetService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.AddSingleton<IEmailSender, OutboxEmailSender>();
        services.AddHostedService<ScanBackgroundService>();
        return services;
    }

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IBusinessLock, BusinessLock>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDataResetService, DataResetService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<AssetService>();
        services.AddScoped<IAssetService>(sp => sp.GetRequiredService<AssetService>());
        services.AddScoped<IAssetExcelService, AssetExcelService>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<IAttachmentService>(sp => sp.GetRequiredService<AttachmentService>());
        services.AddScoped<PartService>();
        services.AddScoped<IPartService>(sp => sp.GetRequiredService<PartService>());
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
        services.AddScoped<IWorkOrderWorkflowService, WorkOrderWorkflowService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<ISlaService, SlaService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ILocationService, LocationService>();
        return services;
    }

    /// <summary>啟動時即載入所有資料檔，格式錯誤可立即發現。</summary>
    public static void LoadAllRepositories(this IServiceProvider services)
    {
        foreach (var type in JsonFileNames.EntityTypes)
            services.GetRequiredService(typeof(IRepository<>).MakeGenericType(type));
    }
}
