using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;

namespace AssetCare.Application.Dtos;

/// <summary>工單清單分頁：我的報修、待派工、我的待辦、全部。</summary>
public enum WorkOrderScope { Mine, Open, Todo, All }

public class WorkOrderQuery
{
    /// <summary>未指定時依角色決定。</summary>
    public WorkOrderScope? Scope { get; set; }
    public string? Keyword { get; set; }
    public WorkOrderStatus? Status { get; set; }
    public Priority? Priority { get; set; }
    public WorkOrderType? Type { get; set; }
    public bool OverdueOnly { get; set; }
    public int Page { get; set; } = 1;
}

public record WorkOrderListItem(Guid Id, int Version, string OrderNo, WorkOrderType Type, WorkOrderStatus Status,
    Priority Priority, string Title, string AssetNo, string AssetName, string LocationName, string? RequesterName,
    string? AssigneeName, DateTime CreatedAt, DateTime ResolveDueAt, bool ResponseOverdue, bool ResolveOverdue,
    bool CanAccept, bool CanStart);

public record WorkOrderList(WorkOrderScope Scope, IReadOnlyList<WorkOrderScope> AvailableScopes,
    PagedResult<WorkOrderListItem> Result);

public record ReportAsset(Guid Id, string AssetNo, string Name, string CategoryName, AssetStatus Status,
    string LocationPath, string? CustodianName);

public record RelatedWorkOrder(Guid Id, string OrderNo, string Title, WorkOrderStatus Status, DateTime CreatedAt,
    string? RequesterName);

/// <summary>報修頁資料：資產資訊與近期重複報修。</summary>
public record ReportContext(ReportAsset Asset, IReadOnlyList<RelatedWorkOrder> Recent, int DuplicateDays);

public record ReportInput(Guid AssetId, string? Description, Priority Priority);

public record WorkOrderPermissions(bool Assign, bool Reassign, bool Accept, bool Start, bool Wait, bool Resume,
    bool Complete, bool Verify, bool Cancel, bool IssuePart, bool SendOut, bool ReturnFromVendor, bool Upload,
    bool Checklist);

public record WorkOrderLogItem(WorkOrderAction Action, WorkOrderStatus? FromStatus, WorkOrderStatus? ToStatus,
    string? Comment, string ActorName, DateTime OccurredAt);

public record WorkOrderPartItem(Guid PartId, string PartNo, string Name, string Unit, int Quantity, decimal UnitCost,
    string IssuedByName, DateTime IssuedAt);

public record VendorRepairInfo(string VendorName, string? VendorPhone, DateOnly? SentOutDate,
    DateOnly? ExpectedReturnDate, DateOnly? ReturnedDate, decimal? Cost);

/// <summary>
/// 工單詳情。IsLimited 表示申請人檢視他人的近期報修：只有摘要，不含歷程、照片、用料與外送資訊。
/// </summary>
public record WorkOrderDetail(Guid Id, int Version, string OrderNo, WorkOrderType Type, WorkOrderStatus Status,
    Priority Priority, string Title, string Description, ReportAsset Asset, string? RequesterName,
    string? RequesterPhone, string? AssigneeName, DateTime CreatedAt,
    DateTime? ResponseDueAt, DateTime? RespondedAt, bool ResponseOverdue,
    DateTime ResolveDueAt, DateTime? CompletedAt, bool ResolveOverdue,
    DateTime? StartedAt, DateTime? ClosedAt, DateTime? CancelledAt, string? CancelReason,
    string? FaultCause, string? Resolution, decimal LaborHours, int ReworkCount,
    IReadOnlyList<ChecklistItem> Checklist, bool IsLimited, VendorRepairInfo? Vendor,
    IReadOnlyList<WorkOrderLogItem> Logs, IReadOnlyList<WorkOrderPartItem> Parts,
    IReadOnlyList<AttachmentItem> Photos, IReadOnlyList<RelatedWorkOrder> Recent, int DuplicateDays,
    WorkOrderPermissions Can, IReadOnlyList<Option> Technicians, IReadOnlyList<PartOption> PartOptions,
    IReadOnlyList<Option> VendorOptions, DateOnly Today, Guid? MaintenancePlanId);

public record CompleteContext(Guid Id, int Version, string OrderNo, string Title, string AssetName,
    string? FaultCause, string? Resolution, decimal LaborHours, IReadOnlyList<PartOption> PartOptions,
    WorkOrderType Type, IReadOnlyList<ChecklistItem> Checklist);

public record CompleteInput(string? FaultCause, string? Resolution, decimal LaborHours,
    IReadOnlyList<PartLine> Parts, IReadOnlyList<UploadedFile> Photos,
    IReadOnlyList<ChecklistEntry>? Checklist = null);

/// <summary>檢查清單單一項目的填寫內容，順序同工單清單。</summary>
public record ChecklistEntry(bool IsChecked, string? Note);

public record SendOutInput(Guid VendorId, DateOnly SentOutDate, DateOnly ExpectedReturnDate, string? Note);

public record VendorReturnInput(DateOnly ReturnedDate, decimal Cost, string? Note);
