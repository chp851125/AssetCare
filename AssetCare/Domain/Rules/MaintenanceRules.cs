using System.Globalization;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Rules;

public static class MaintenanceRules
{
    /// <summary>即將到期的最短提示天數（提前產生天數較長時以其為準）。</summary>
    private const int MinSoonDays = 7;

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

    /// <summary>提前產生天數上限：必須短於一個週期。</summary>
    public static int MaxLeadDays(MaintenanceCycle cycle) => cycle switch
    {
        MaintenanceCycle.Weekly => 6,
        MaintenanceCycle.Monthly => 27,
        MaintenanceCycle.Quarterly => 88,
        MaintenanceCycle.Yearly => 364,
        _ => 0
    };

    public static string DateKey(DateOnly due) => due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string UsageKey(decimal usage) => "usage:" + usage.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>計畫目前這一期的週期識別；條件不完整時回傳 null。</summary>
    public static string? CurrentCycleKey(MaintenancePlan plan) => plan.Cycle == MaintenanceCycle.ByUsage
        ? plan.NextDueUsage is { } usage ? UsageKey(usage) : null
        : plan.NextDueDate is { } date ? DateKey(date) : null;

    /// <summary>
    /// 是否到了產生本期工單的時間：日期週期為「下次保養日前 N 天」（含當天）起；
    /// 依使用量為累計使用量達到下次保養使用量。
    /// </summary>
    public static bool IsReadyToGenerate(MaintenancePlan plan, DateOnly today, decimal? currentUsage)
    {
        if (!plan.IsActive) return false;
        if (plan.Cycle == MaintenanceCycle.ByUsage)
            return plan.NextDueUsage is { } next && currentUsage >= next;
        return plan.NextDueDate is { } due && today >= due.AddDays(-plan.LeadDays);
    }

    /// <summary>保養日當天仍不算逾期；依使用量的計畫達到使用量即視為逾期。</summary>
    public static MaintenanceDueState DueStateOf(MaintenancePlan plan, DateOnly today, decimal? currentUsage)
    {
        if (!plan.IsActive) return MaintenanceDueState.Inactive;
        if (plan.Cycle == MaintenanceCycle.ByUsage)
        {
            if (plan.NextDueUsage is not { } next) return MaintenanceDueState.Normal;
            if (currentUsage >= next) return MaintenanceDueState.Overdue;
            // 剩餘不到一成間隔視為即將到期。
            return next - (currentUsage ?? 0) <= (plan.UsageInterval ?? 0) * 0.1m
                ? MaintenanceDueState.DueSoon
                : MaintenanceDueState.Normal;
        }
        if (plan.NextDueDate is not { } due) return MaintenanceDueState.Normal;
        if (due < today) return MaintenanceDueState.Overdue;
        // 週期短於提示天數時（每日、每週），只以提前產生天數判斷，避免永遠顯示即將到期。
        var window = plan.Cycle is MaintenanceCycle.Daily or MaintenanceCycle.Weekly
            ? plan.LeadDays
            : Math.Max(plan.LeadDays, MinSoonDays);
        return due.DayNumber - today.DayNumber <= window ? MaintenanceDueState.DueSoon : MaintenanceDueState.Normal;
    }

    /// <summary>完工後的下次保養日：由完成日推算，並跳過已產生過工單的日期。</summary>
    public static DateOnly NextDateAfter(MaintenanceCycle cycle, DateOnly from, IReadOnlySet<string> usedKeys)
    {
        var next = NextDueDate(cycle, from) ?? throw new InvalidOperationException("依使用量的計畫沒有日期週期。");
        while (usedKeys.Contains(DateKey(next))) next = NextDueDate(cycle, next)!.Value;
        return next;
    }

    /// <summary>
    /// 完工後的下次保養使用量：由目前使用量（不低於本期門檻）加上間隔，並跳過已產生過工單的數值。
    /// </summary>
    public static decimal NextUsageAfter(decimal threshold, decimal? currentUsage, decimal interval,
        IReadOnlySet<string> usedKeys)
    {
        if (interval <= 0) throw new InvalidOperationException("使用量間隔必須大於 0。");
        var next = Math.Max(threshold, currentUsage ?? 0) + interval;
        while (usedKeys.Contains(UsageKey(next))) next += interval;
        return next;
    }
}
