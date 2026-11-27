namespace AssetCare.Application.Dtos;

public enum SettingKind { Number, Boolean, Extensions }

public record SettingItem(Guid Id, int Version, string Key, string Group, string Description, string Value,
    SettingKind Kind, int Min, int Max);

public record SettingChange(Guid Id, int Version, string? Value);

public record AuditQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>帳號、對象、說明或 IP。</summary>
    public string? Keyword { get; set; }
    public string? ActionCode { get; set; }
    public string? EntityType { get; set; }
    public int Page { get; set; } = 1;
}

public record AuditItem(DateTime OccurredAt, string UserName, string? DisplayName, string IpAddress,
    string ActionCode, string EntityType, string? EntityName, string? Detail);

public record AuditSearchResult(PagedResult<AuditItem> Result, IReadOnlyList<string> EntityTypes);
