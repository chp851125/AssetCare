using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;

namespace AssetCare.Application.Services;

public class AuditQueryService(
    IRepository<AuditLog> logs,
    IRepository<User> users,
    ISpreadsheetService spreadsheet,
    ICurrentUser currentUser,
    IAuditService audit,
    ITimeProvider time) : IAuditQueryService
{
    private const int PageSize = 10;

    public AuditSearchResult Search(AuditQuery query)
    {
        currentUser.RequireAdmin();
        var entityTypes = logs.GetAll().Select(l => l.EntityType).Distinct()
            .Order(StringComparer.Ordinal).ToList();
        return new AuditSearchResult(PagedResult<AuditItem>.From(Filter(query), query.Page, PageSize), entityTypes);
    }

    public byte[] Export(AuditQuery query)
    {
        currentUser.RequireAdmin();
        var rows = Filter(query).Select(i => (IReadOnlyList<object?>)
        [
            time.ToLocal(i.OccurredAt), i.UserName, i.DisplayName, i.IpAddress,
            AuditActions.Label(i.ActionCode), i.EntityType, i.EntityName, i.Detail
        ]).ToList();
        audit.Record(AuditActions.Export, nameof(AuditLog), detail: $"匯出稽核紀錄 {rows.Count} 筆");
        return spreadsheet.Write(new SheetData("稽核紀錄",
            ["時間", "帳號", "姓名", "IP", "動作", "對象類型", "對象", "說明"], rows));
    }

    private List<AuditItem> Filter(AuditQuery query)
    {
        var names = users.GetAll().ToDictionary(u => u.Id, u => u.DisplayName);
        IEnumerable<AuditLog> source = logs.GetAll();
        // 日期條件為台北日期，含首尾兩天。
        if (query.From is { } from)
            source = source.Where(l => DateOnly.FromDateTime(time.ToLocal(l.OccurredAt)) >= from);
        if (query.To is { } to)
            source = source.Where(l => DateOnly.FromDateTime(time.ToLocal(l.OccurredAt)) <= to);
        if (!string.IsNullOrEmpty(query.ActionCode)) source = source.Where(l => l.Action == query.ActionCode);
        if (!string.IsNullOrEmpty(query.EntityType)) source = source.Where(l => l.EntityType == query.EntityType);

        var items = source.OrderByDescending(l => l.OccurredAt).Select(l => new AuditItem(l.OccurredAt, l.UserName,
            l.UserId is { } id ? names.GetValueOrDefault(id) : null, l.IpAddress, l.Action, l.EntityType,
            l.EntityName, l.Detail));
        if (query.Keyword?.Trim() is { Length: > 0 } keyword)
            items = items.Where(i => new[] { i.UserName, i.DisplayName, i.EntityName, i.Detail, i.IpAddress }
                .Any(v => v?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true));
        return items.ToList();
    }
}
