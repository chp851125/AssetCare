using AssetCare.Domain.Enums;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Dtos;

public enum AssetSort { AssetNo, Name, Category, Status, Location, PurchaseDate, Warranty }

/// <summary>清單查詢條件（清單、匯出與標籤列印共用）。</summary>
public class AssetQuery
{
    public string? Keyword { get; set; }
    public Guid? CategoryId { get; set; }
    /// <summary>含所有下層地點。</summary>
    public Guid? LocationId { get; set; }
    public Guid? DepartmentId { get; set; }
    public AssetStatus? Status { get; set; }
    public WarrantyState? Warranty { get; set; }
    public AssetSort Sort { get; set; }
    public bool Desc { get; set; }
    public int Page { get; set; } = 1;
}

public record AssetListItem(Guid Id, string AssetNo, string Name, string CategoryName, AssetStatus Status,
    string LocationName, string? CustodianName, DateOnly PurchaseDate, DateOnly? WarrantyEndDate,
    WarrantyState Warranty);

public record AssetFilterOptions(IReadOnlyList<Option> Categories, IReadOnlyList<Option> Locations,
    IReadOnlyList<Option> Departments);

public record CategoryOption(Guid Id, string Code, string Name, int DefaultUsefulLifeYears);

public record AssetFormOptions(IReadOnlyList<CategoryOption> Categories, IReadOnlyList<Option> Locations,
    IReadOnlyList<Option> Departments, IReadOnlyList<Option> Users, IReadOnlyList<Option> Vendors);

/// <summary>新增與編輯資產的輸入。編輯時忽略類別、地點、部門與保管人（類別不可改，其餘走轉移）。</summary>
public record AssetInput(string? Name, Guid CategoryId, AssetStatus Status, string? Brand, string? Model,
    string? SerialNo, DateOnly PurchaseDate, decimal PurchaseCost, decimal SalvageValue, int UsefulLifeYears,
    DateOnly? WarrantyEndDate, Guid LocationId, Guid? DepartmentId, Guid? CustodianUserId, Guid? VendorId,
    string? Description, string? UsageUnit, decimal? CurrentUsage);

public record AssetEditData(Guid Id, int Version, string AssetNo, AssetInput Input);

public record TransferInput(Guid LocationId, Guid? DepartmentId, Guid? CustodianUserId, string? Reason);

/// <summary>轉移與報廢頁面所需的資產現況。</summary>
public record AssetSummary(Guid Id, int Version, string AssetNo, string Name, AssetStatus Status,
    Guid LocationId, string LocationPath, Guid? DepartmentId, string? DepartmentName,
    Guid? CustodianUserId, string? CustodianName, int ActiveWorkOrderCount);

public record AssetWorkOrderItem(Guid Id, string OrderNo, WorkOrderType Type, WorkOrderStatus Status,
    Priority Priority, string Title, string? AssigneeName, DateTime CreatedAt);

public record AssetPlanItem(Guid Id, string Name, MaintenanceCycle Cycle, DateOnly? NextDueDate,
    decimal? NextDueUsage, DateOnly? LastCompletedDate, string? AssigneeName, bool IsActive, bool IsOverdue);

public record AssetTransferItem(DateTime TransferredAt, string FromText, string ToText, string Reason, string ByName);

public record AssetFinance(decimal PurchaseCost, decimal SalvageValue, int UsefulLifeYears,
    Depreciation Depreciation, DateOnly AsOf);

/// <summary>
/// 資產詳情。依角色裁切：非員工看不到轉移紀錄與附件、只看得到自己的工單；僅主管以上有財務資料。
/// </summary>
public record AssetDetail(Guid Id, int Version, string AssetNo, string Name, string CategoryName, AssetStatus Status,
    string? Brand, string? Model, string? SerialNo, string? Description,
    DateOnly PurchaseDate, DateOnly? WarrantyEndDate, WarrantyState Warranty, int? WarrantyDaysLeft,
    string LocationPath, string? DepartmentName, string? CustodianName,
    string? VendorName, string? VendorPhone, decimal? CurrentUsage, string? UsageUnit,
    DateTime? ScrappedAt, string? ScrapReason,
    AssetFinance? Finance,
    IReadOnlyList<AssetWorkOrderItem> WorkOrders,
    IReadOnlyList<AssetPlanItem> Plans,
    IReadOnlyList<AssetTransferItem> Transfers,
    IReadOnlyList<AttachmentItem> Attachments);

public record AssetLabel(Guid Id, string AssetNo, string Name, string LocationName, string Url, byte[] QrPng);

public record ImportError(int RowNumber, string Message);

/// <summary>匯入採全有或全無：有任何錯誤時 AssetNos 為空。</summary>
public record ImportResult(IReadOnlyList<string> AssetNos, IReadOnlyList<ImportError> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}
