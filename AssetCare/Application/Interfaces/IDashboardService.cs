using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

public interface IDashboardService
{
    /// <summary>依目前使用者角色回傳其可見範圍的摘要數字。</summary>
    HomeSummary GetHomeSummary();
}
