using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>資產類別維護（僅限主管以上）。</summary>
public interface ICategoryService
{
    IReadOnlyList<CategoryListItem> GetList();
    CategoryEditData GetForEdit(Guid id);
    void Create(CategoryInput input);
    void Update(Guid id, int version, CategoryInput input);
    /// <summary>僅限沒有任何資產使用的類別。</summary>
    void Delete(Guid id, int version);
}
