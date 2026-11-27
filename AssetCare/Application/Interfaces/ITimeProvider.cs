namespace AssetCare.Application.Interfaces;

/// <summary>系統唯一的時間來源。禁止在他處使用 DateTime.Now / UtcNow。</summary>
public interface ITimeProvider
{
    DateTime UtcNow { get; }
    /// <summary>台北時區的今天，用於比較日期型欄位（保固、保養日等）。</summary>
    DateOnly Today { get; }
    DateTime ToLocal(DateTime utc);
    DateTime ToUtc(DateTime local);
}
