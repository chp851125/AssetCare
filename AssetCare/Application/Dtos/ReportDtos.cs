using System.ComponentModel.DataAnnotations;

namespace AssetCare.Application.Dtos;

public enum ReportType
{
    [Display(Name = "工單明細")] WorkOrders,
    [Display(Name = "資產清冊")] Assets,
    [Display(Name = "保養紀錄")] Maintenance,
    [Display(Name = "零件異動")] PartTransactions,
    [Display(Name = "維修成本")] Costs
}

/// <summary>報表期間（台北日期，含首尾兩天）。</summary>
public record ReportRange(DateOnly From, DateOnly To);
