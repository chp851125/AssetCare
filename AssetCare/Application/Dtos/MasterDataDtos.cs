namespace AssetCare.Application.Dtos;

public record CategoryListItem(Guid Id, int Version, string Code, string Name, string? Description,
    int DefaultUsefulLifeYears, bool IsActive, int AssetCount);

public record CategoryInput(string? Code, string? Name, string? Description, int DefaultUsefulLifeYears, bool IsActive);

public record CategoryEditData(Guid Id, int Version, bool CodeLocked, CategoryInput Input);

public record LocationNode(Guid Id, int Version, string Code, string Name, int Depth, bool IsActive,
    int AssetCount, bool CanDelete);

public record LocationInput(string? Code, string? Name, Guid? ParentId, int SortOrder, bool IsActive);

public record LocationEditData(Guid Id, int Version, LocationInput Input);
