using System.Collections;
using System.Text.Json;
using AssetCare.Domain.Entities;

namespace AssetCare.Infrastructure.Json.Seeding;

/// <summary>一整組範例資料（各實體清單與上傳檔內容）。</summary>
public class SeedDataSet
{
    private readonly Dictionary<Type, IList> _sets = JsonFileNames.EntityTypes
        .ToDictionary(t => t, t => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(t))!);

    public List<T> Set<T>() where T : BaseEntity => (List<T>)_sets[typeof(T)];

    public IEnumerable<BaseEntity> All => _sets.Values.SelectMany(l => l.Cast<BaseEntity>());

    /// <summary>uploads 資料夾中的檔案：實體檔名 → 內容。</summary>
    public Dictionary<string, byte[]> Uploads { get; } = [];

    public static SeedDataSet Load(string seedDirectory)
    {
        var data = new SeedDataSet();
        foreach (var type in JsonFileNames.EntityTypes)
        {
            var path = Path.Combine(seedDirectory, JsonFileNames.For(type));
            if (!File.Exists(path)) throw new FileNotFoundException($"缺少資料檔 {path}");
            using var stream = File.OpenRead(path);
            var list = (IList)JsonSerializer.Deserialize(stream, data._sets[type].GetType(), JsonDefaults.Options)!;
            foreach (var item in list) data._sets[type].Add(item);
        }

        var uploads = Path.Combine(seedDirectory, JsonDataStore.UploadsFolder);
        if (Directory.Exists(uploads))
            foreach (var file in Directory.EnumerateFiles(uploads))
                data.Uploads[Path.GetFileName(file)] = File.ReadAllBytes(file);
        return data;
    }

    public void Save(string seedDirectory)
    {
        Directory.CreateDirectory(seedDirectory);
        foreach (var (type, list) in _sets)
        {
            using var stream = File.Create(Path.Combine(seedDirectory, JsonFileNames.For(type)));
            JsonSerializer.Serialize(stream, list, list.GetType(), JsonDefaults.Options);
        }

        var uploads = Path.Combine(seedDirectory, JsonDataStore.UploadsFolder);
        if (Directory.Exists(uploads)) Directory.Delete(uploads, recursive: true);
        Directory.CreateDirectory(uploads);
        foreach (var (name, content) in Uploads)
            File.WriteAllBytes(Path.Combine(uploads, name), content);
    }
}
