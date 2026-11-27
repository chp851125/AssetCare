namespace AssetCare.Application.Interfaces;

public interface IDataResetService
{
    /// <summary>僅限系統管理員。</summary>
    void ResetToSampleData();
}
