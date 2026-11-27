using System.Linq.Expressions;
using System.Text.Json;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Exceptions;

namespace AssetCare.Infrastructure.Json;

/// <summary>
/// 以單一 JSON 檔保存實體：啟動時載入記憶體，異動時寫暫存檔再取代原檔。需註冊為 Singleton。
/// </summary>
public class JsonFileRepository<T> : IRepository<T>, IReloadableRepository where T : BaseEntity
{
    private readonly JsonDataStore _store;
    private readonly ITimeProvider _time;
    private readonly string _path;
    private readonly Lock _lock = new();
    private Dictionary<Guid, T> _items = [];

    public JsonFileRepository(JsonDataStore store, ITimeProvider time)
    {
        _store = store;
        _time = time;
        _path = Path.Combine(store.DataDirectory, JsonFileNames.For(typeof(T)));
        _store.Gate.EnterReadLock();
        try
        {
            Load();
            _store.Register(this);
        }
        finally { _store.Gate.ExitReadLock(); }
    }

    public IReadOnlyList<T> GetAll() => Locked(() => _items.Values.Select(Clone).ToList());

    public T? GetById(Guid id) => Locked(() => _items.TryGetValue(id, out var item) ? Clone(item) : null);

    public IReadOnlyList<T> Query(Expression<Func<T, bool>> predicate)
    {
        var filter = predicate.Compile();
        return Locked(() => _items.Values.Where(filter).Select(Clone).ToList());
    }

    public T Add(T entity) => Locked(() =>
    {
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (_items.ContainsKey(entity.Id)) throw new ConcurrencyException("資料已存在。");
        entity.Version = 1;
        entity.CreatedAt = entity.UpdatedAt = _time.UtcNow;
        Commit(new Dictionary<Guid, T>(_items) { [entity.Id] = Clone(entity) });
        return entity;
    });

    public void Update(T entity) => Locked(() =>
    {
        var current = FindForWrite(entity);
        var copy = Clone(entity);
        copy.Version = current.Version + 1;
        copy.CreatedAt = current.CreatedAt;
        copy.UpdatedAt = _time.UtcNow;
        Commit(new Dictionary<Guid, T>(_items) { [copy.Id] = copy });
        entity.Version = copy.Version;
        entity.CreatedAt = copy.CreatedAt;
        entity.UpdatedAt = copy.UpdatedAt;
        return true;
    });

    public void Delete(T entity) => Locked(() =>
    {
        FindForWrite(entity);
        var next = new Dictionary<Guid, T>(_items);
        next.Remove(entity.Id);
        Commit(next);
        return true;
    });

    /// <summary>僅由 JsonDataStore 在持有寫入鎖時呼叫。</summary>
    void IReloadableRepository.Reload()
    {
        lock (_lock) Load();
    }

    private T FindForWrite(T entity)
    {
        if (!_items.TryGetValue(entity.Id, out var current))
            throw new ConcurrencyException("資料已被其他人刪除，請重新整理頁面。");
        if (current.Version != entity.Version) throw new ConcurrencyException();
        return current;
    }

    private TResult Locked<TResult>(Func<TResult> action)
    {
        _store.Gate.EnterReadLock();
        try
        {
            lock (_lock) return action();
        }
        finally
        {
            _store.Gate.ExitReadLock();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            _items = [];
            return;
        }
        using var stream = File.OpenRead(_path);
        var list = JsonSerializer.Deserialize<List<T>>(stream, JsonDefaults.Options) ?? [];
        _items = list.ToDictionary(x => x.Id);
    }

    /// <summary>先寫檔成功才更新記憶體，避免兩者不一致。</summary>
    private void Commit(Dictionary<Guid, T> next)
    {
        var temp = _path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, next.Values.ToList(), JsonDefaults.Options);
            stream.Flush(flushToDisk: true);
        }
        ReplaceWithRetry(temp, _path);
        _items = next;
    }

    // Windows 上防毒或索引程式可能短暫鎖住目標檔。
    private static void ReplaceWithRetry(string source, string target)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(source, target, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 5)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }

    private static T Clone(T item) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(item, JsonDefaults.Options), JsonDefaults.Options)!;
}
