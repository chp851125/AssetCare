using AssetCare.Domain.Enums;

namespace AssetCare.Infrastructure.Json.Seeding;

/// <summary>SeedData/_source/catalog.json 的結構：所有範例文字與情境定義都在該檔，不寫在程式中。</summary>
public record SeedCatalog(
    string DemoPassword,
    string EmailDomain,
    List<CatalogDepartment> Departments,
    List<CatalogUser> Users,
    List<CatalogLocation> Locations,
    List<CatalogCategory> Categories,
    List<CatalogVendor> Vendors,
    List<CatalogAsset> Assets,
    List<CatalogTransfer> Transfers,
    List<CatalogPart> Parts,
    List<CatalogStockIn> StockIns,
    List<CatalogPlan> Plans,
    List<CatalogWorkOrder> WorkOrders,
    List<CatalogAssetAttachment> AssetAttachments,
    List<CatalogNotification> Notifications,
    List<CatalogAuditLog> AuditLogs,
    List<CatalogSetting> Settings);

public record CatalogDepartment(string Key, string Name, string? Manager);
public record CatalogUser(string Key, string Name, UserRole Role, string Dept, string Phone, bool Disabled = false);
public record CatalogLocation(string Key, string Name, string? Parent);
public record CatalogCategory(string Key, string Name, int Life, string Description);
public record CatalogVendor(string Key, string Name, string TaxId, string Contact, string Phone, string Address, string Scope);

public record CatalogAsset(string Key, string Category, string Name, string Brand, string Model, string Location,
    string Dept, string? Custodian, string Vendor, decimal Cost, int AgeMonths, string Warranty,
    int? WarrantyOffsetDays = null, string? State = null, int? ScrappedDaysAgo = null, string? ScrapReason = null,
    string? UsageUnit = null, decimal? CurrentUsage = null);

public record CatalogTransfer(string Asset, string FromLocation, string FromDept, string FromCustodian,
    string Reason, string By, int DaysAgo);

public record CatalogPart(string Key, string Name, string Spec, string Unit, decimal Cost, int Safety, int Stock,
    string Vendor, string Shelf);

public record CatalogStockIn(string Part, int Qty, int DaysAgo, string By, string Note);

public record CatalogPlan(string Key, string Name, string Asset, MaintenanceCycle Cycle, int LeadDays, string Assignee,
    Priority Priority, List<string> Checklist, int? DueOffsetDays = null, int? LastOffsetDays = null,
    decimal? UsageInterval = null, decimal? NextDueUsage = null);

public record CatalogWorkOrder(string Key, string Asset, Priority Priority, string? Requester, double CreatedHoursAgo,
    string Title, string Description, List<CatalogStep> Steps, string? Plan = null, string? Cause = null,
    string? Resolution = null, decimal LaborHours = 0, decimal VendorCost = 0, int Photos = 0);

/// <param name="At">相對於工單建立時間的小時數。</param>
public record CatalogStep(string Do, double At, string? By = null, string? To = null, string? Note = null,
    string? Part = null, int Qty = 0, string? Vendor = null, int ReturnInDays = 0);

public record CatalogAssetAttachment(string Asset, string FileName, string Caption, string By, int DaysAgo);

public record CatalogNotification(string User, NotificationType Type, string Title, string Message, double HoursAgo,
    string? WorkOrder = null, string? Asset = null, string? Part = null, string? Plan = null,
    bool Read = false, bool Email = false, bool EmailFailed = false);

public record CatalogAuditLog(string User, string Action, string Entity, double HoursAgo, string? Detail = null,
    string? WorkOrder = null, string? Asset = null, string? Part = null, string? TargetUser = null);

public record CatalogSetting(string Key, string Value, string Description);
