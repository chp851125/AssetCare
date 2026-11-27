using AssetCare.Domain.Enums;

namespace AssetCare.Application.Dtos;

public record SummaryCard(string Title, int Value, string Icon, string Tone);

public record DashboardOrderItem(Guid Id, string OrderNo, string Title, string AssetName, WorkOrderStatus Status,
    Priority Priority, DateTime ResolveDueAt, bool Overdue);

public record DashboardPlanItem(Guid Id, string Name, string AssetName, DateOnly? NextDueDate,
    string? AssigneeName, MaintenanceDueState State);

/// <summary>首頁的清單區塊。</summary>
public record DashboardOrderList(string Title, string EmptyText, IReadOnlyList<DashboardOrderItem> Items, int Total);

/// <summary>
/// 首頁內容依角色裁切：申請人只有自己的報修；技術員為自己的待辦與今日保養；主管以上為全局。
/// OverduePlans 僅技術員（自己負責）與主管（全部）有內容。
/// </summary>
public record HomeSummary(IReadOnlyList<SummaryCard> Cards, IReadOnlyList<DashboardOrderList> Lists,
    IReadOnlyList<DashboardPlanItem> OverduePlans);

public record NamedCount(string Name, int Count);

public record TrendPoint(string Label, int Repair, int Preventive);

public record CostPoint(string Label, decimal Parts, decimal Vendor);

/// <summary>MtbfDays：期間內相鄰兩次故障通報的平均間隔天數；少於 2 次為 null。</summary>
public record FaultRank(Guid AssetId, string AssetNo, string AssetName, int Count, double? MtbfDays);

/// <summary>平均時間與達成率沒有樣本時為 null。</summary>
public record DashboardKpi(int RepairCount, double? AverageResponseHours, double? AverageResolveHours,
    double? SlaRate, int SlaMet, int SlaTotal, decimal TotalCost);

public record DashboardAnalytics(int Days, IReadOnlyList<int> DayOptions, DashboardKpi Kpi,
    IReadOnlyList<TrendPoint> Trend, IReadOnlyList<NamedCount> Categories, IReadOnlyList<NamedCount> Departments,
    IReadOnlyList<CostPoint> Costs, IReadOnlyList<FaultRank> Ranking);
