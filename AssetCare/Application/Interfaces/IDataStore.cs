namespace AssetCare.Application.Interfaces;

/// <summary>整體資料庫層級的維運操作。</summary>
public interface IDataStore
{
    /// <summary>清除目前資料並還原為範例資料。</summary>
    void ResetToSeed();
}
