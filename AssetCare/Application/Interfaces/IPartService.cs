using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>查詢限員工；維護、入庫與盤點限主管以上。領用走工單流程。</summary>
public interface IPartService
{
    PagedResult<PartListItem> Search(PartQuery query);
    PartDetail GetDetail(Guid id);
    IReadOnlyList<Option> GetVendorOptions();
    PartEditData GetForEdit(Guid id);
    Guid Create(PartInput input);
    void Update(Guid id, int version, PartInput input);
    void StockIn(Guid id, int version, int quantity, decimal unitCost, string? note);
    /// <summary>盤點調整為實際數量。</summary>
    void Adjust(Guid id, int version, int actualQuantity, string? note);
}
