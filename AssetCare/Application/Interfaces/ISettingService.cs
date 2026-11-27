using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>SLA 與系統參數，限系統管理員。</summary>
public interface ISettingService
{
    IReadOnlyList<SettingItem> GetAll();
    /// <summary>全部檢查通過才寫入；回傳實際變更的項目數。</summary>
    int Update(IReadOnlyList<SettingChange> changes);
}
