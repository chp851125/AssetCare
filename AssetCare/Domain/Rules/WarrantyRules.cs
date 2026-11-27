using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Rules;

public static class WarrantyRules
{
    /// <summary>到期日當天仍在保固內；剩餘天數 ≤ noticeDays 視為即將到期。</summary>
    public static WarrantyState StateOf(DateOnly? endDate, DateOnly today, int noticeDays) => endDate switch
    {
        null => WarrantyState.None,
        { } end when end < today => WarrantyState.Expired,
        { } end when end.DayNumber - today.DayNumber <= noticeDays => WarrantyState.Expiring,
        _ => WarrantyState.Active
    };
}
