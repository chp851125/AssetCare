namespace AssetCare.Domain.Rules;

public record Depreciation(
    int ElapsedMonths, int TotalMonths, decimal MonthlyAmount, decimal AccumulatedAmount, decimal BookValue);

/// <summary>
/// 直線法折舊，以「滿月」計算。帳面價值固定介於殘值與成本之間，且不會小於 0。
/// </summary>
public static class DepreciationCalculator
{
    public static Depreciation Calculate(
        decimal cost, decimal salvageValue, int usefulLifeYears, DateOnly purchaseDate, DateOnly asOf)
    {
        cost = Math.Max(cost, 0);
        var salvage = Math.Clamp(salvageValue, 0, cost);
        if (usefulLifeYears <= 0) return new Depreciation(0, 0, 0, 0, cost);

        var totalMonths = usefulLifeYears * 12;
        var elapsed = Math.Min(FullMonthsBetween(purchaseDate, asOf), totalMonths);
        var depreciable = cost - salvage;
        // 先乘後除，避免期滿時因除不盡而殘留尾數。
        var accumulated = Math.Clamp(
            Math.Round(depreciable * elapsed / totalMonths, 0, MidpointRounding.AwayFromZero), 0, depreciable);
        var monthly = Math.Round(depreciable / totalMonths, 0, MidpointRounding.AwayFromZero);
        return new Depreciation(elapsed, totalMonths, monthly, accumulated, cost - accumulated);
    }

    private static int FullMonthsBetween(DateOnly from, DateOnly to)
    {
        if (to <= from) return 0;
        var months = (to.Year - from.Year) * 12 + to.Month - from.Month;
        return from.AddMonths(months) > to ? months - 1 : months;
    }
}
