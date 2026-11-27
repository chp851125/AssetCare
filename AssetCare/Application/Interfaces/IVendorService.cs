using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>廠商資料維護（僅限主管以上）。廠商不刪除，以停用代替。</summary>
public interface IVendorService
{
    PagedResult<VendorListItem> Search(VendorQuery query);
    VendorEditData GetForEdit(Guid id);
    void Create(VendorInput input);
    void Update(Guid id, int version, VendorInput input);
}
