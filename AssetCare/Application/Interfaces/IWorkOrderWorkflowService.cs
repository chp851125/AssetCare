using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>工單異動。每個操作都檢查角色、擁有權與狀態；非法轉移丟出 BusinessRuleException。</summary>
public interface IWorkOrderWorkflowService
{
    Guid Report(ReportInput input, IReadOnlyList<UploadedFile> photos);
    /// <summary>主管以上。未指派時為派工，已指派時為改派。</summary>
    void Assign(Guid id, int version, Guid technicianId, string? note);
    /// <summary>技術員接下未指派的工單。</summary>
    void Accept(Guid id, int version);
    void Start(Guid id, int version);
    void WaitForParts(Guid id, int version, string? reason);
    void Resume(Guid id, int version, string? note);
    void Complete(Guid id, int version, CompleteInput input);
    void Verify(Guid id, int version, string? note);
    void Reject(Guid id, int version, string? reason);
    void Cancel(Guid id, int version, string? reason);
    void AddComment(Guid id, string? comment);
    /// <summary>保養工單檢查清單勾選（處理中或待料中）。</summary>
    void UpdateChecklist(Guid id, int version, IReadOnlyList<ChecklistEntry> entries);
    void IssuePart(Guid id, int version, PartLine line);
    void SendToVendor(Guid id, int version, SendOutInput input);
    void ReturnFromVendor(Guid id, int version, VendorReturnInput input);
}
