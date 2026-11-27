using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

public interface IAssetService
{
    PagedResult<AssetListItem> Search(AssetQuery query);
    AssetFilterOptions GetFilterOptions();
    AssetDetail GetDetail(Guid id);
    Guid GetIdByAssetNo(string assetNo);

    /// <summary>以下維護操作僅限主管以上。</summary>
    AssetFormOptions GetFormOptions();
    AssetEditData GetForEdit(Guid id);
    Guid Create(AssetInput input);
    void Update(Guid id, int version, AssetInput input);
    AssetSummary GetSummary(Guid id);
    void Scrap(Guid id, int version, string? reason);
    void Transfer(Guid id, int version, TransferInput input);

    /// <summary>
    /// QR 標籤資料（僅限員工）。ids 有值時依指定資產，否則依查詢條件的全部結果（不含已報廢）。
    /// QR 內容為 {baseUrl}/Assets/ByNo/{資產編號}。
    /// </summary>
    IReadOnlyList<AssetLabel> GetLabels(IReadOnlyCollection<Guid> ids, AssetQuery query, string baseUrl);
}
