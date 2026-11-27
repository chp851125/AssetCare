using AssetCare.Domain.Enums;

namespace AssetCare.Application.Dtos;

public class PartQuery
{
    public string? Keyword { get; set; }
    public bool LowStockOnly { get; set; }
    public bool IncludeInactive { get; set; }
    public int Page { get; set; } = 1;
}

public record PartListItem(Guid Id, string PartNo, string Name, string? Spec, string Unit, int StockQuantity,
    int SafetyStock, decimal UnitCost, string? StorageLocation, bool IsActive, bool IsLowStock);

public record PartTransactionItem(DateTime OccurredAt, PartTransactionType Type, int Quantity, int BalanceAfter,
    decimal UnitCost, Guid? WorkOrderId, string? OrderNo, string OperatorName, string? Note);

/// <summary>可領用零件的工單；Version 供領用時做並行檢查。</summary>
public record IssueTarget(Guid Id, int Version, string Text);

public record PartDetail(Guid Id, int Version, string PartNo, string Name, string? Spec, string Unit,
    int StockQuantity, int SafetyStock, decimal UnitCost, string? VendorName, string? VendorPhone,
    string? StorageLocation, bool IsActive, bool IsLowStock,
    IReadOnlyList<PartTransactionItem> Transactions, IReadOnlyList<IssueTarget> IssueTargets);

/// <summary>InitialStock 僅新增時使用；之後庫存只能透過入庫、領用與盤點調整變更。</summary>
public record PartInput(string? Name, string? Spec, string? Unit, int SafetyStock, decimal UnitCost, Guid? VendorId,
    string? StorageLocation, bool IsActive, int InitialStock);

public record PartEditData(Guid Id, int Version, string PartNo, PartInput Input);

/// <summary>領用零件的下拉選項。</summary>
public record PartOption(Guid Id, string Text, int Stock);

public record PartLine(Guid PartId, int Quantity);
