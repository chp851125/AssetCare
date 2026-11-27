using AssetCare.Domain.Enums;

namespace AssetCare.Application;

/// <summary>AppSetting 的鍵值名稱；實際值存於 AppSettings.json。</summary>
public static class SettingKeys
{
    public static string SlaResponseHours(Priority p) => $"sla.{p.ToString().ToLowerInvariant()}.responseHours";
    public static string SlaResolveHours(Priority p) => $"sla.{p.ToString().ToLowerInvariant()}.resolveHours";
    public const string AutoSetAssetUnderRepair = "workOrder.autoSetAssetUnderRepair";
    public const string DuplicateReportDays = "workOrder.duplicateReportDays";
    public const string MaintenanceDefaultLeadDays = "maintenance.defaultLeadDays";
    public const string WarrantyNoticeDays = "warranty.noticeDays";
    public const string UploadMaxSizeMb = "upload.maxSizeMb";
    public const string UploadAllowedExtensions = "upload.allowedExtensions";
    public const string MaxFailedLogins = "security.maxFailedLogins";
    public const string LockoutMinutes = "security.lockoutMinutes";
    public const string PasswordMinLength = "security.passwordMinLength";
}
