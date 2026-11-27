using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class DashboardService(
    IRepository<WorkOrder> workOrders,
    IRepository<WorkOrderPart> orderParts,
    IRepository<Asset> assets,
    IRepository<AssetCategory> categories,
    IRepository<Department> departments,
    IRepository<User> users,
    IRepository<Part> parts,
    IRepository<MaintenancePlan> plans,
    ICurrentUser currentUser,
    ITimeProvider time) : IDashboardService
{
    private const int ListSize = 5;
    private const int RankingSize = 10;
    private const int DefaultDays = 90;
    private static readonly int[] DayOptions = [30, 90, 180, 365];

    public HomeSummary GetHomeSummary()
    {
        var userId = currentUser.RequireUserId();
        var now = time.UtcNow;
        var today = time.Today;
        var all = workOrders.GetAll();
        var assetMap = assets.GetAll().ToDictionary(a => a.Id);

        bool Overdue(WorkOrder w) => WorkOrderStateMachine.IsActive(w.Status) &&
            (WorkOrderRules.IsResponseOverdue(w, now) || WorkOrderRules.IsResolveOverdue(w, now));
        bool Working(WorkOrder w) =>
            w.Status is WorkOrderStatus.Assigned or WorkOrderStatus.InProgress or WorkOrderStatus.WaitingParts;

        DashboardOrderList List(string title, string empty, IEnumerable<WorkOrder> source)
        {
            var items = source.ToList();
            return new DashboardOrderList(title, empty, items.Take(ListSize).Select(w => new DashboardOrderItem(
                w.Id, w.OrderNo, w.Title, assetMap.GetValueOrDefault(w.AssetId)?.Name ?? "—", w.Status, w.Priority,
                w.ResolveDueAt, Overdue(w))).ToList(), items.Count);
        }

        switch (currentUser.Role)
        {
            case UserRole.Requester:
            {
                var mine = all.Where(w => w.RequesterUserId == userId).ToList();
                return new HomeSummary(
                    [
                        new("處理中的報修", mine.Count(w => w.Status == WorkOrderStatus.Open || Working(w)), "bi-tools", "primary"),
                        new("待我驗收", mine.Count(w => w.Status == WorkOrderStatus.Completed), "bi-clipboard-check", "warning"),
                        new("已結案", mine.Count(w => w.Status == WorkOrderStatus.Closed), "bi-check-circle", "success")
                    ],
                    [
                        List("待我驗收", "目前沒有待驗收的工單。",
                            mine.Where(w => w.Status == WorkOrderStatus.Completed).OrderBy(w => w.CompletedAt)),
                        List("我的報修", "尚未提出報修。", mine.OrderByDescending(w => w.CreatedAt))
                    ], []);
            }
            case UserRole.Technician:
            {
                var assigned = all.Where(w => w.AssigneeUserId == userId).ToList();
                // 今日保養：指派給自己、尚未完工且完工期限在今天（含）以前。
                var maintenance = assigned.Where(w => w.Type == WorkOrderType.Preventive && Working(w) &&
                        DateOnly.FromDateTime(time.ToLocal(w.ResolveDueAt)) <= today)
                    .OrderBy(w => w.ResolveDueAt).ToList();
                return new HomeSummary(
                    [
                        new("我的待辦", assigned.Count(Working), "bi-list-task", "primary"),
                        new("今日保養", maintenance.Count, "bi-calendar-check", "success"),
                        new("可接單", all.Count(w => w.Status == WorkOrderStatus.Open), "bi-hand-index", "info"),
                        new("我的逾時工單", assigned.Count(Overdue), "bi-alarm", "danger")
                    ],
                    [
                        List("今日保養（含逾期）", "今天沒有待執行的保養。", maintenance),
                        List("我的待辦", "目前沒有待辦工單。",
                            assigned.Where(Working).OrderBy(w => w.ResolveDueAt).ThenByDescending(w => w.Priority))
                    ],
                    OverduePlans(p => p.AssigneeUserId == userId, assetMap, today));
            }
            case UserRole.Supervisor:
            case UserRole.Admin:
            {
                var overduePlans = OverduePlans(_ => true, assetMap, today);
                return new HomeSummary(
                    [
                        new("待派工", all.Count(w => w.Status == WorkOrderStatus.Open), "bi-inbox", "info"),
                        new("進行中工單", all.Count(Working), "bi-tools", "primary"),
                        new("逾時工單", all.Count(Overdue), "bi-alarm", "danger"),
                        new("逾期保養", overduePlans.Count, "bi-calendar-x", "danger"),
                        new("維修中資產", assetMap.Values.Count(a => a.Status == AssetStatus.UnderRepair), "bi-pc-display", "warning"),
                        new("低於安全庫存", parts.GetAll().Count(PartRules.IsLowStock), "bi-box-seam", "danger")
                    ],
                    [
                        List("待派工", "目前沒有待派工的工單。",
                            all.Where(w => w.Status == WorkOrderStatus.Open).OrderBy(w => w.ResolveDueAt)),
                        List("逾時工單", "目前沒有逾時工單。", all.Where(Overdue).OrderBy(w => w.ResolveDueAt))
                    ], overduePlans);
            }
            default:
                throw new ForbiddenException();
        }
    }

    public DashboardAnalytics GetAnalytics(int? days)
    {
        currentUser.RequireManager();
        var period = days is { } d && DayOptions.Contains(d) ? d : DefaultDays;
        var today = time.Today;
        var from = today.AddDays(-(period - 1));
        var now = time.UtcNow;

        DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(time.ToLocal(utc));

        var assetMap = assets.GetAll().ToDictionary(a => a.Id);
        var created = workOrders.GetAll().Where(w => LocalDate(w.CreatedAt) >= from).ToList();
        // 故障統計只計報修且未取消的工單。
        var repairs = created.Where(w => w.Type == WorkOrderType.Repair && w.Status != WorkOrderStatus.Cancelled)
            .ToList();

        var response = repairs.Where(w => w.RespondedAt != null)
            .Select(w => (w.RespondedAt!.Value - w.CreatedAt).TotalHours).ToList();
        var resolve = repairs.Where(w => w.CompletedAt != null)
            .Select(w => (w.CompletedAt!.Value - w.CreatedAt).TotalHours).ToList();
        // SLA 樣本：已完工，或尚未完工但已確定逾時者；仍在時限內的進行中工單不列入。
        var judged = repairs.Select(w => (
                Missed: WorkOrderRules.IsResponseOverdue(w, now) || WorkOrderRules.IsResolveOverdue(w, now),
                Done: w.CompletedAt != null))
            .Where(x => x.Missed || x.Done).ToList();
        var met = judged.Count(x => !x.Missed);

        var costs = CostEvents(from, today);
        var kpi = new DashboardKpi(repairs.Count, Average(response), Average(resolve),
            judged.Count == 0 ? null : (double)met / judged.Count, met, judged.Count,
            costs.Sum(c => c.Parts + c.Vendor));

        var categoryNames = categories.GetAll().ToDictionary(c => c.Id, c => c.Name);
        var byCategory = Count(repairs.Select(w => assetMap.TryGetValue(w.AssetId, out var a)
            ? categoryNames.GetValueOrDefault(a.CategoryId, "未分類")
            : "未分類"));

        var departmentNames = departments.GetAll().ToDictionary(x => x.Id, x => x.Name);
        var userDepartments = users.GetAll().ToDictionary(u => u.Id, u => u.DepartmentId);
        var byDepartment = Count(repairs.Select(w =>
            w.RequesterUserId is { } r && userDepartments.GetValueOrDefault(r) is { } dep
                ? departmentNames.GetValueOrDefault(dep, "未指定")
                : "未指定"));

        var ranking = repairs.GroupBy(w => w.AssetId)
            .Select(g =>
            {
                var times = g.Select(w => w.CreatedAt).Order().ToList();
                double? mtbf = times.Count < 2
                    ? null
                    : Math.Round((times[^1] - times[0]).TotalDays / (times.Count - 1), 1);
                var asset = assetMap.GetValueOrDefault(g.Key);
                return new FaultRank(g.Key, asset?.AssetNo ?? "—", asset?.Name ?? "—", times.Count, mtbf);
            })
            .OrderByDescending(r => r.Count).ThenBy(r => r.MtbfDays ?? double.MaxValue)
            .ThenBy(r => r.AssetNo, StringComparer.Ordinal)
            .Take(RankingSize).ToList();

        return new DashboardAnalytics(period, DayOptions, kpi, Trend(created, from, today, period, LocalDate),
            byCategory, byDepartment, MonthlyCosts(costs, from, today), ranking);
    }

    private List<DashboardPlanItem> OverduePlans(Func<MaintenancePlan, bool> filter,
        Dictionary<Guid, Asset> assetMap, DateOnly today)
    {
        var names = users.GetAll().ToDictionary(u => u.Id, u => u.DisplayName);
        return plans.GetAll().Where(filter)
            .Select(p => (Plan: p, Asset: assetMap.GetValueOrDefault(p.AssetId)))
            .Where(x => MaintenanceRules.DueStateOf(x.Plan, today, x.Asset?.CurrentUsage) ==
                        MaintenanceDueState.Overdue)
            .OrderBy(x => x.Plan.NextDueDate ?? DateOnly.MaxValue)
            .Select(x => new DashboardPlanItem(x.Plan.Id, x.Plan.Name, x.Asset?.Name ?? "—", x.Plan.NextDueDate,
                x.Plan.AssigneeUserId is { } id ? names.GetValueOrDefault(id) : null, MaintenanceDueState.Overdue))
            .ToList();
    }

    /// <summary>用料成本依領用日、外送費用依回廠日歸屬（定義同維修成本報表）。</summary>
    private List<(DateOnly Date, decimal Parts, decimal Vendor)> CostEvents(DateOnly from, DateOnly to)
    {
        var events = orderParts.GetAll()
            .Select(p => (Date: DateOnly.FromDateTime(time.ToLocal(p.IssuedAt)), Parts: p.Quantity * p.UnitCost,
                Vendor: 0m))
            .ToList();
        foreach (var order in workOrders.GetAll())
            if (WorkOrderRules.VendorCostDate(order) is { } date)
                events.Add((date, 0m, order.VendorCost));
        return events.Where(e => e.Date >= from && e.Date <= to).ToList();
    }

    /// <summary>30 天以內按日、180 天以內按週（週一起）、更長按月。</summary>
    private static List<TrendPoint> Trend(List<WorkOrder> orders, DateOnly from, DateOnly to, int period,
        Func<DateTime, DateOnly> localDate)
    {
        Func<DateOnly, DateOnly> bucket = period <= 30 ? date => date
            : period <= 180 ? date => date.AddDays(-(((int)date.DayOfWeek + 6) % 7))
            : date => new DateOnly(date.Year, date.Month, 1);
        var format = period > 180 ? "yyyy/MM" : "MM/dd";

        var grouped = orders.ToLookup(w => bucket(localDate(w.CreatedAt)));
        return Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(offset => bucket(from.AddDays(offset))).Distinct()
            .Select(key => new TrendPoint(key.ToString(format),
                grouped[key].Count(w => w.Type == WorkOrderType.Repair && w.Status != WorkOrderStatus.Cancelled),
                grouped[key].Count(w => w.Type == WorkOrderType.Preventive && w.Status != WorkOrderStatus.Cancelled)))
            .ToList();
    }

    private static List<CostPoint> MonthlyCosts(List<(DateOnly Date, decimal Parts, decimal Vendor)> costs,
        DateOnly from, DateOnly to)
    {
        var result = new List<CostPoint>();
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            var items = costs.Where(c => c.Date.Year == month.Year && c.Date.Month == month.Month).ToList();
            result.Add(new CostPoint(month.ToString("yyyy/MM"), items.Sum(c => c.Parts), items.Sum(c => c.Vendor)));
        }
        return result;
    }

    private static List<NamedCount> Count(IEnumerable<string> names) => names.CountBy(n => n)
        .OrderByDescending(p => p.Value).ThenBy(p => p.Key)
        .Select(p => new NamedCount(p.Key, p.Value)).ToList();

    private static double? Average(List<double> values) =>
        values.Count == 0 ? null : Math.Round(values.Average(), 1);
}
