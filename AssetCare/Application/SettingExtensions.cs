using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;

namespace AssetCare.Application;

public static class SettingExtensions
{
    public static string? GetValue(this IRepository<AppSetting> settings, string key) =>
        settings.Query(s => s.Key == key).FirstOrDefault()?.Value;

    /// <summary>讀取正整數參數；缺少或格式錯誤時回傳預設值。</summary>
    public static int GetInt(this IRepository<AppSetting> settings, string key, int fallback) =>
        int.TryParse(settings.GetValue(key), out var value) && value > 0 ? value : fallback;
}
