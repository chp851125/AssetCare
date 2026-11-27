using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class MaintenancePlanService(
    IRepository<MaintenancePlan> plans,
    IRepository<WorkOrder> workOrders,
    IRepository<Asset> assets,
    IRepository<User> users,
    IRepository<AppSetting> settings,
    IMaintenanceScheduler scheduler,
    IBusinessLock businessLock,
    ICurrentUser currentUser,
    IAuditService audit,
    ITimeProvider time) : IMaintenancePlanService
{
    private const int PageSize = 10;
    private const int MaxChecklistItems = 30;
    private const int MaxCalendarDays = 62;

    public PagedResult<PlanListItem> Search(PlanQuery query)
    {
        currentUser.RequireStaff();
        var today = time.Today;
        var assetMap = assets.GetAll().ToDictionary(a => a.Id);
        var names = UserNames();

        IEnumerable<MaintenancePlan> source = plans.GetAll();
        if (query.Cycle is { } cycle) source = source.Where(p => p.Cycle == cycle);
        if (query.Mine) source = source.Where(p => p.AssigneeUserId == currentUser.UserId);
        if (query.Keyword?.Trim() is { Length: > 0 } keyword)
            source = source.Where(p =>
            {
                var asset = assetMap.GetValueOrDefault(p.AssetId);
                return new[] { p.Name, asset?.AssetNo, asset?.Name }
                    .Any(v => v?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true);
            });

        var items = source.Select(p =>
        {
            var asset = assetMap.GetValueOrDefault(p.AssetId);
            return new PlanListItem(p.Id, p.Name, asset?.AssetNo ?? "—", asset?.Name ?? "—", p.Cycle, p.NextDueDate,
                p.NextDueUsage, asset?.CurrentUsage, asset?.UsageUnit, p.LastCompletedDate,
                Name(names, p.AssigneeUserId), MaintenanceRules.DueStateOf(p, today, asset?.CurrentUsage));
        });
        if (query.State is { } state) items = items.Where(i => i.State == state);

        // 逾期優先，其次即將到期，停用排最後。
        var ordered = items.OrderBy(i => i.State switch
            {
                MaintenanceDueState.Overdue => 0,
                MaintenanceDueState.DueSoon => 1,
                MaintenanceDueState.Normal => 2,
                _ => 3
            })
            .ThenBy(i => i.NextDueDate ?? DateOnly.MaxValue).ThenBy(i => i.Name).ToList();
        return PagedResult<PlanListItem>.From(ordered, query.Page, PageSize);
    }

    public PlanDetail GetDetail(Guid id)
    {
        currentUser.RequireStaff();
        var plan = Find(id);
        var asset = assets.GetById(plan.AssetId) ?? throw new NotFoundException("找不到資產。");
        var names = UserNames();
        var now = time.UtcNow;
        var history = workOrders.Query(w => w.MaintenancePlanId == id)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new PlanOrderItem(w.Id, w.OrderNo, w.MaintenanceCycleKey ?? "—", w.Status,
                Name(names, w.AssigneeUserId), w.CreatedAt, w.CompletedAt,
                WorkOrderRules.IsResolveOverdue(w, now)))
            .ToList();

        return new PlanDetail(plan.Id, plan.Version, plan.Name, asset.Id, asset.AssetNo, asset.Name, asset.Version,
            plan.Cycle, plan.Priority, plan.NextDueDate, plan.LeadDays, plan.LastCompletedDate, plan.UsageInterval,
            plan.NextDueUsage, asset.CurrentUsage, asset.UsageUnit, Name(names, plan.AssigneeUserId),
            plan.ChecklistItems, plan.IsActive,
            MaintenanceRules.DueStateOf(plan, time.Today, asset.CurrentUsage), history,
            currentUser.IsManager(),
            plan.Cycle == MaintenanceCycle.ByUsage && asset.Status != AssetStatus.Scrapped && CanRecord(plan));
    }

    public PlanEditData GetForEdit(Guid id)
    {
        currentUser.RequireManager();
        var plan = Find(id);
        var asset = assets.GetById(plan.AssetId) ?? throw new NotFoundException("找不到資產。");
        AssetRules.EnsureNotScrapped(asset, "維護保養計畫");
        return new PlanEditData(plan.Id, plan.Version, $"{asset.AssetNo} {asset.Name}",
            new PlanInput(plan.AssetId, plan.Name, plan.Cycle, plan.Priority, plan.NextDueDate, plan.LeadDays,
                plan.UsageInterval, plan.NextDueUsage, plan.AssigneeUserId,
                string.Join("\n", plan.ChecklistItems), plan.IsActive));
    }

    public PlanFormOptions GetFormOptions()
    {
        currentUser.RequireManager();
        return new PlanFormOptions(
            assets.Query(a => a.Status != AssetStatus.Scrapped).OrderBy(a => a.AssetNo, StringComparer.Ordinal)
                .Select(a => new Option(a.Id, $"{a.AssetNo} {a.Name}")).ToList(),
            users.Query(u => u.IsActive && u.Role == UserRole.Technician).OrderBy(u => u.UserName)
                .Select(u => new Option(u.Id, u.DisplayName)).ToList(),
            settings.GetNonNegativeInt(SettingKeys.MaintenanceDefaultLeadDays, 7));
    }

    public Guid Create(PlanInput input)
    {
        currentUser.RequireManager();
        return businessLock.Run(() =>
        {
            var plan = new MaintenancePlan { AssetId = input.AssetId };
            Apply(plan, input);
            plans.Add(plan);
            audit.Record(AuditActions.Create, nameof(MaintenancePlan), plan.Id, plan.Name, "新增保養計畫");
            return plan.Id;
        });
    }

    public void Update(Guid id, int version, PlanInput input)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var plan = Find(id);
            // 資產建立後不可更換（歷史工單屬於原資產）。
            Apply(plan, input with { AssetId = plan.AssetId });
            plan.Version = version;
            plans.Update(plan);
            audit.Record(AuditActions.Update, nameof(MaintenancePlan), plan.Id, plan.Name, "修改保養計畫");
        });
    }

    public void SetActive(Guid id, int version, bool isActive)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var plan = Find(id);
            if (isActive)
            {
                var asset = assets.GetById(plan.AssetId) ?? throw new NotFoundException("找不到資產。");
                AssetRules.EnsureNotScrapped(asset, "啟用保養計畫");
            }
            plan.IsActive = isActive;
            plan.Version = version;
            plans.Update(plan);
            audit.Record(isActive ? AuditActions.Activate : AuditActions.Deactivate, nameof(MaintenancePlan),
                plan.Id, plan.Name);
        });
    }

    public int RecordUsage(Guid planId, int assetVersion, decimal usage)
    {
        currentUser.RequireStaff();
        businessLock.Run(() =>
        {
            var plan = Find(planId);
            if (!CanRecord(plan)) throw new ForbiddenException("只能登錄自己負責計畫的使用量。");
            if (plan.Cycle != MaintenanceCycle.ByUsage)
                throw new BusinessRuleException("只有依使用量的計畫需要登錄使用量。");
            var asset = assets.GetById(plan.AssetId) ?? throw new NotFoundException("找不到資產。");
            AssetRules.EnsureNotScrapped(asset, "登錄使用量");
            if (usage > 1_000_000_000m) throw new BusinessRuleException("使用量超出合理範圍。");
            if (usage < (asset.CurrentUsage ?? 0))
                throw new BusinessRuleException($"累計使用量不可小於目前值 {asset.CurrentUsage:0.##}。");

            var previous = asset.CurrentUsage;
            asset.CurrentUsage = Math.Round(usage, 2);
            asset.Version = assetVersion;
            assets.Update(asset);
            audit.Record(AuditActions.RecordUsage, nameof(Asset), asset.Id, asset.AssetNo,
                $"累計使用量 {previous ?? 0:0.##} → {asset.CurrentUsage:0.##} {asset.UsageUnit}");
        });
        return scheduler.GenerateDueOrders();
    }

    public int GenerateNow()
    {
        currentUser.RequireManager();
        return scheduler.GenerateDueOrders();
    }

    public IReadOnlyList<CalendarEvent> GetCalendar(DateOnly start, DateOnly end)
    {
        currentUser.RequireStaff();
        if (end <= start || end.DayNumber - start.DayNumber > MaxCalendarDays)
            throw new BusinessRuleException("行事曆查詢範圍不正確。");

        var today = time.Today;
        var assetMap = assets.GetAll().ToDictionary(a => a.Id);
        var orders = workOrders.Query(w => w.Type == WorkOrderType.Preventive);
        var events = new List<CalendarEvent>();

        // 已產生的工單放在完工時限那一天。
        foreach (var order in orders.Where(w => w.Status != WorkOrderStatus.Cancelled))
        {
            var date = DateOnly.FromDateTime(time.ToLocal(order.ResolveDueAt));
            if (date < start || date >= end) continue;
            var overdue = MaintenanceScheduler.IsPending(order.Status) && date < today;
            events.Add(new CalendarEvent(
                $"{(overdue ? "逾期" : order.Status.DisplayName())}｜{order.Title}",
                MaintenanceRules.DateKey(date), WorkOrderLinks.Details(order.Id), overdue ? "overdue" : "order"));
        }

        // 尚未產生工單的日期週期：由下次保養日往後推估。
        foreach (var plan in plans.Query(p => p.IsActive && p.NextDueDate != null))
        {
            if (plan.Cycle == MaintenanceCycle.ByUsage ||
                assetMap.GetValueOrDefault(plan.AssetId) is not { Status: not AssetStatus.Scrapped }) continue;
            var used = orders.Where(w => w.MaintenancePlanId == plan.Id)
                .Select(w => w.MaintenanceCycleKey).ToHashSet();
            for (var date = plan.NextDueDate!.Value; date < end;
                 date = MaintenanceRules.NextDueDate(plan.Cycle, date)!.Value)
            {
                if (date < start || used.Contains(MaintenanceRules.DateKey(date))) continue;
                // 過去的日期只有「下次保養日」是真的逾期，之後的推估日不顯示。
                if (date < today && date != plan.NextDueDate) continue;
                var overdue = date < today;
                events.Add(new CalendarEvent($"{(overdue ? "逾期" : "預定")}｜{plan.Name}",
                    MaintenanceRules.DateKey(date), $"/MaintenancePlans/Details/{plan.Id}",
                    overdue ? "overdue" : "planned"));
            }
        }
        return events.OrderBy(e => e.Start, StringComparer.Ordinal).ToList();
    }

    /// <summary>驗證並套用表單內容；呼叫端需持有 IBusinessLock。</summary>
    private void Apply(MaintenancePlan plan, PlanInput input)
    {
        var asset = assets.GetById(plan.AssetId) ?? throw new BusinessRuleException("請選擇資產。");
        AssetRules.EnsureNotScrapped(asset, "維護保養計畫");
        var name = InputText.Required(input.Name, "計畫名稱", 100);
        if (!Enum.IsDefined(input.Cycle)) throw new BusinessRuleException("請選擇保養週期。");
        if (!Enum.IsDefined(input.Priority)) throw new BusinessRuleException("請選擇優先級。");

        var checklist = (input.Checklist ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (checklist.Count == 0) throw new BusinessRuleException("請至少填寫一個檢查項目（一行一項）。");
        if (checklist.Count > MaxChecklistItems)
            throw new BusinessRuleException($"檢查項目最多 {MaxChecklistItems} 項。");
        if (checklist.Any(c => c.Length > 100)) throw new BusinessRuleException("每個檢查項目不可超過 100 個字。");

        // 沿用原本的負責人時不重新檢查（可能已停用，產生工單時會改為待派工）。
        if (input.AssigneeUserId is { } assignee && assignee != plan.AssigneeUserId &&
            users.GetById(assignee) is not { IsActive: true, Role: UserRole.Technician })
            throw new BusinessRuleException("負責人必須是啟用中的技術員。");

        var isNew = plan.Id == Guid.Empty;
        var previousKey = isNew ? null : MaintenanceRules.CurrentCycleKey(plan);

        plan.Name = name;
        plan.Cycle = input.Cycle;
        plan.Priority = input.Priority;
        plan.AssigneeUserId = input.AssigneeUserId;
        plan.ChecklistItems = checklist;
        plan.IsActive = input.IsActive;

        if (input.Cycle == MaintenanceCycle.ByUsage)
        {
            if (input.UsageInterval is not (>= 0.01m and <= 1_000_000_000m))
                throw new BusinessRuleException("請填寫大於 0 的使用量間隔。");
            if (input.NextDueUsage is not (>= 0.01m and <= 1_000_000_000m))
                throw new BusinessRuleException("請填寫下次保養的累計使用量。");
            plan.UsageInterval = Math.Round(input.UsageInterval.Value, 2);
            plan.NextDueUsage = Math.Round(input.NextDueUsage.Value, 2);
            plan.NextDueDate = null;
            plan.LeadDays = 0;
        }
        else
        {
            if (input.NextDueDate is not { } due) throw new BusinessRuleException("請填寫下次保養日。");
            if (due.Year is < 2000 or > 2200) throw new BusinessRuleException("下次保養日超出合理範圍。");
            var max = MaintenanceRules.MaxLeadDays(input.Cycle);
            if (input.LeadDays < 0 || input.LeadDays > max)
                throw new BusinessRuleException(
                    $"「{input.Cycle.DisplayName()}」計畫的提前產生天數須介於 0 到 {max} 天。");
            plan.NextDueDate = due;
            plan.LeadDays = input.LeadDays;
            plan.UsageInterval = null;
            plan.NextDueUsage = null;
        }

        // 改成已產生過工單的週期會使計畫停滯，因此拒絕。
        var key = MaintenanceRules.CurrentCycleKey(plan);
        if (!isNew && key != previousKey &&
            workOrders.Query(w => w.MaintenancePlanId == plan.Id).Any(w => w.MaintenanceCycleKey == key))
            throw new BusinessRuleException("此保養日（或使用量）已產生過工單，請改用其他數值。");
    }

    private bool CanRecord(MaintenancePlan plan) =>
        currentUser.IsManager() || (currentUser.IsStaff() && plan.AssigneeUserId == currentUser.UserId);

    private Dictionary<Guid, string> UserNames() => users.GetAll().ToDictionary(u => u.Id, u => u.DisplayName);

    private static string? Name(Dictionary<Guid, string> names, Guid? id) =>
        id is { } value ? names.GetValueOrDefault(value) : null;

    private MaintenancePlan Find(Guid id) =>
        plans.GetById(id) ?? throw new NotFoundException("找不到保養計畫。");
}
