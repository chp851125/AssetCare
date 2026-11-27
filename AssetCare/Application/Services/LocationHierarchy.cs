using AssetCare.Domain.Entities;

namespace AssetCare.Application.Services;

/// <summary>地點階層（廠區 → 樓層 → 區域）的走訪工具。</summary>
internal class LocationHierarchy(IReadOnlyList<Location> locations)
{
    private readonly Dictionary<Guid, Location> _byId = locations.ToDictionary(l => l.Id);
    private readonly ILookup<Guid, Location> _children = locations.ToLookup(l => l.ParentId ?? Guid.Empty);

    public string NameOf(Guid id) => _byId.TryGetValue(id, out var l) ? l.Name : "—";

    /// <summary>由最上層到自身的完整路徑。</summary>
    public string PathOf(Guid id)
    {
        var names = new List<string>();
        var visited = new HashSet<Guid>(); // 人工修改資料造成循環時避免無窮迴圈
        Guid? current = id;
        while (current is { } c && visited.Add(c) && _byId.TryGetValue(c, out var location))
        {
            names.Insert(0, location.Name);
            current = location.ParentId;
        }
        return names.Count == 0 ? "—" : string.Join(" / ", names);
    }

    /// <summary>自身與所有下層。</summary>
    public HashSet<Guid> SelfAndDescendants(Guid id)
    {
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>([id]);
        while (queue.TryDequeue(out var current))
            if (result.Add(current))
                foreach (var child in _children[current]) queue.Enqueue(child.Id);
        return result;
    }

    /// <summary>深度優先、依排序值排列。</summary>
    public IEnumerable<(Location Location, int Depth)> Ordered()
    {
        var visited = new HashSet<Guid>();
        IEnumerable<(Location, int)> Walk(IEnumerable<Location> level, int depth)
        {
            foreach (var location in level.OrderBy(l => l.SortOrder).ThenBy(l => l.Code))
            {
                if (!visited.Add(location.Id)) continue;
                yield return (location, depth);
                foreach (var child in Walk(_children[location.Id], depth + 1)) yield return child;
            }
        }
        // 上層不存在者視為最上層，避免從清單中消失。
        return Walk(locations.Where(l => l.ParentId is not { } p || !_byId.ContainsKey(p)), 0);
    }

    /// <summary>下拉選單文字以全形空白縮排表現階層。</summary>
    public static string Indent(string name, int depth) => new string('　', depth) + name;
}
