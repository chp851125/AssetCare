using System.Linq.Expressions;
using AssetCare.Domain.Entities;

namespace AssetCare.Application.Interfaces;

/// <summary>
/// 資料存取抽象。回傳的物件一律是複本，修改後須呼叫 Update 才會保存。
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    IReadOnlyList<T> GetAll();
    T? GetById(Guid id);
    IReadOnlyList<T> Query(Expression<Func<T, bool>> predicate);
    /// <summary>新增；Id 為空時自動產生，並設定 Version、CreatedAt、UpdatedAt。</summary>
    T Add(T entity);
    /// <summary>以 entity.Version 做樂觀並行檢查，衝突時丟出 ConcurrencyException。成功後回寫新版本號。</summary>
    void Update(T entity);
    /// <summary>以 entity.Version 做樂觀並行檢查後刪除。</summary>
    void Delete(T entity);
}
