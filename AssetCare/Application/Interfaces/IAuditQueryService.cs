using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>稽核紀錄查詢與匯出，限系統管理員。寫入用 IAuditService。</summary>
public interface IAuditQueryService
{
    AuditSearchResult Search(AuditQuery query);
    byte[] Export(AuditQuery query);
}
