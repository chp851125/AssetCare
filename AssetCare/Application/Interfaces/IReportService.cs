using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>Excel 報表匯出，限主管以上。</summary>
public interface IReportService
{
    /// <summary>預設期間：近 90 天。</summary>
    ReportRange GetDefaultRange();
    /// <summary>匯出報表內容。資產清冊為目前資料，不受期間限制。</summary>
    byte[] Export(ReportType type, ReportRange range);
}
