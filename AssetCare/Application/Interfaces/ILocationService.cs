using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>地點維護（僅限主管以上）。</summary>
public interface ILocationService
{
    /// <summary>依階層排序。</summary>
    IReadOnlyList<LocationNode> GetTree();
    /// <summary>可作為上層的地點；編輯時排除自身與下層。</summary>
    IReadOnlyList<Option> GetParentOptions(Guid? editingId);
    LocationEditData GetForEdit(Guid id);
    void Create(LocationInput input);
    void Update(Guid id, int version, LocationInput input);
    /// <summary>僅限沒有下層地點、資產與轉移紀錄的地點。</summary>
    void Delete(Guid id, int version);
}
