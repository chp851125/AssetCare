using System.Text.RegularExpressions;
using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Exceptions;

namespace AssetCare.Application.Services;

public partial class CategoryService(
    IRepository<AssetCategory> categories,
    IRepository<Asset> assets,
    IBusinessLock businessLock,
    ICurrentUser currentUser,
    IAuditService audit) : ICategoryService
{
    [GeneratedRegex("^[A-Z0-9]{2,6}$")]
    private static partial Regex CodePattern();

    public IReadOnlyList<CategoryListItem> GetList()
    {
        currentUser.RequireManager();
        var counts = assets.GetAll().CountBy(a => a.CategoryId).ToDictionary();
        return categories.GetAll().OrderBy(c => c.Code)
            .Select(c => new CategoryListItem(c.Id, c.Version, c.Code, c.Name, c.Description,
                c.DefaultUsefulLifeYears, c.IsActive, counts.GetValueOrDefault(c.Id)))
            .ToList();
    }

    public CategoryEditData GetForEdit(Guid id)
    {
        currentUser.RequireManager();
        var c = Find(id);
        return new CategoryEditData(c.Id, c.Version, IsUsed(id),
            new CategoryInput(c.Code, c.Name, c.Description, c.DefaultUsefulLifeYears, c.IsActive));
    }

    public void Create(CategoryInput input)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var category = new AssetCategory();
            Apply(category, input);
            categories.Add(category);
            audit.Record(AuditActions.Create, nameof(AssetCategory), category.Id, category.Name, "新增資產類別");
        });
    }

    public void Update(Guid id, int version, CategoryInput input)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var category = Find(id);
            // 代碼已寫入資產編號，使用中的類別不可更改。
            if (IsUsed(id)) input = input with { Code = category.Code };
            Apply(category, input);
            category.Version = version;
            categories.Update(category);
            audit.Record(AuditActions.Update, nameof(AssetCategory), category.Id, category.Name, "修改資產類別");
        });
    }

    public void Delete(Guid id, int version)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var category = Find(id);
            if (IsUsed(id)) throw new BusinessRuleException("此類別已有資產使用，無法刪除，可改為停用。");
            category.Version = version;
            categories.Delete(category);
            audit.Record(AuditActions.Delete, nameof(AssetCategory), category.Id, category.Name, "刪除資產類別");
        });
    }

    private AssetCategory Find(Guid id) => categories.GetById(id) ?? throw new NotFoundException("找不到資產類別。");

    private bool IsUsed(Guid id) => assets.Query(a => a.CategoryId == id).Count > 0;

    private void Apply(AssetCategory category, CategoryInput input)
    {
        var code = InputText.Required(input.Code, "類別代碼", 6).ToUpperInvariant();
        if (!CodePattern().IsMatch(code)) throw new BusinessRuleException("類別代碼須為 2 到 6 碼英文或數字。");
        var name = InputText.Required(input.Name, "類別名稱", 50);
        var others = categories.GetAll().Where(c => c.Id != category.Id).ToList();
        if (others.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException($"類別代碼 {code} 已存在。");
        if (others.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException($"類別名稱「{name}」已存在。");
        if (input.DefaultUsefulLifeYears is < 1 or > 100)
            throw new BusinessRuleException("預設耐用年限須為 1 到 100 年。");

        category.Code = code;
        category.Name = name;
        category.Description = InputText.Optional(input.Description, "說明", 200);
        category.DefaultUsefulLifeYears = input.DefaultUsefulLifeYears;
        category.IsActive = input.IsActive;
    }
}
