using AssetCare.Application.Dtos;

namespace AssetCare.Web.ViewModels;

public class SettingChangeViewModel
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public string? Value { get; set; }
}

/// <summary>系統參數頁；Items 為目前設定，Posted 為送出失敗時保留的輸入。</summary>
public record SettingsViewModel(IReadOnlyList<SettingItem> Items, IReadOnlyDictionary<Guid, string?> Posted);

public record AuditIndexViewModel(AuditQuery Query, AuditSearchResult Search);

public record ReportsViewModel(ReportRange Range);

public record HomeViewModel(HomeSummary Summary, DashboardAnalytics? Analytics);
