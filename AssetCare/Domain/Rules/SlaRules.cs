namespace AssetCare.Domain.Rules;

public static class SlaRules
{
    /// <summary>
    /// 是否逾時：以完成時間（尚未完成則為現在）與時限比較；剛好等於時限不算逾時。參數皆為 UTC。
    /// </summary>
    public static bool IsOverdue(DateTime? dueAtUtc, DateTime? doneAtUtc, DateTime nowUtc) =>
        dueAtUtc is { } due && (doneAtUtc ?? nowUtc) > due;
}
