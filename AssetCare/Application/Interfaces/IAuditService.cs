namespace AssetCare.Application.Interfaces;

public interface IAuditService
{
    /// <summary>以目前使用者與來源 IP 寫入稽核紀錄；登入等尚未建立身分的情境可指定 actor。</summary>
    void Record(string action, string entityType, Guid? entityId = null, string? entityName = null,
        string? detail = null, Guid? actorUserId = null, string? actorName = null);
}
