using AssetCare.Domain.Entities;

namespace AssetCare.Infrastructure.Json;

public static class JsonFileNames
{
    public static IReadOnlyList<Type> EntityTypes { get; } = typeof(BaseEntity).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(BaseEntity)))
        .OrderBy(t => t.Name)
        .ToList();

    /// <summary>檔名為實體名複數，例如 AssetCategories.json。</summary>
    public static string For(Type entityType)
    {
        var name = entityType.Name;
        var plural = name.EndsWith('y') && !"aeiou".Contains(name[^2]) ? name[..^1] + "ies" : name + "s";
        return plural + ".json";
    }
}
