namespace AssetCare.Application.Interfaces;

/// <summary>
/// 全系統唯一的寫入鎖。需要「先檢查、再寫入」一致性的流程（配號、唯一性、跨實體異動）須在鎖內執行。
/// </summary>
public interface IBusinessLock
{
    T Run<T>(Func<T> action);
    void Run(Action action);
}
