using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

public interface IDashboardService
{
    /// <summary>依目前使用者角色回傳其可見範圍的摘要與清單。</summary>
    HomeSummary GetHomeSummary();
    /// <summary>全局統計圖表，限主管以上。days 不在可選範圍時使用預設值。</summary>
    DashboardAnalytics GetAnalytics(int? days);
}
