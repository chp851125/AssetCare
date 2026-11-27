using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Rules;

public static class MaintenanceRules
{
    /// <summary>依週期由基準日推算下次保養日；依使用量的計畫沒有日期，回傳 null。</summary>
    public static DateOnly? NextDueDate(MaintenanceCycle cycle, DateOnly from) => cycle switch
    {
        MaintenanceCycle.Daily => from.AddDays(1),
        MaintenanceCycle.Weekly => from.AddDays(7),
        MaintenanceCycle.Monthly => from.AddMonths(1),
        MaintenanceCycle.Quarterly => from.AddMonths(3),
        MaintenanceCycle.Yearly => from.AddYears(1),
        _ => null
    };
}
