using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

/// <summary>保養工單的自動產生，以及工單完工、取消後推進計畫週期。</summary>
public class MaintenanceScheduler(
    IRepository<MaintenancePlan> plans,
    IRepository<WorkOrder> workOrders,
    IRepository<WorkOrderLog> logs,
    IRepository<Asset> assets,
    IRepository<User> users,
    IRepository<AppSetting> settings,
    INotificationDispatcher notifications,
    IBusinessLock businessLock,
    IAuditService audit,
    ITimeProvider time) : IMaintenanceScheduler
{
    private const string SystemActor = "系統";

    public int GenerateDueOrders()
    {
        var created = businessLock.Run(() =>
        {
            var result = new List<(WorkOrder Order, Asset Asset)>();
            var today = time.Today;
            foreach (var plan in plans.Query(p => p.IsActive).OrderBy(p => p.NextDueDate))
            {
                if (assets.GetById(plan.AssetId) is not { } asset || asset.Status == AssetStatus.Scrapped) continue;
                if (!MaintenanceRules.IsReadyToGenerate(plan, today, asset.CurrentUsage)) continue;
                if (MaintenanceRules.CurrentCycleKey(plan) is not { } key) continue;

                var existing = workOrders.Query(w => w.MaintenancePlanId == plan.Id);
                // 同一週期只產生一次（含已取消）；前一張尚未完工時也不再產生下一張。
                if (existing.Any(w => w.MaintenanceCycleKey == key || IsPending(w.Status))) continue;

                result.Add((CreateOrder(plan, asset, key), asset));
            }
            return result;
        });

        var managers = notifications.ManagerIds().Select(id => (Guid?)id).ToList();
        foreach (var (order, asset) in created)
        {
            var link = WorkOrderLinks.Details(order.Id);
            var message = $"{asset.Name}「{order.Title}」須於 {time.ToLocal(order.ResolveDueAt):yyyy/MM/dd} 前完成。";
            notifications.Send([order.AssigneeUserId], NotificationType.WorkOrderAssigned,
                $"工單 {order.OrderNo} 派工通知", message, link);
            notifications.Send(managers, NotificationType.MaintenanceDue,
                $"保養工單 {order.OrderNo} 已產生", message, link);
        }
        return created.Count;
    }

    /// <summary>
    /// 保養工單完工後依完成日重算下次保養日。只有該計畫最新一張未取消的工單會推進計畫，
    /// 避免舊工單重做時覆蓋較新週期。呼叫端需持有 IBusinessLock。
    /// </summary>
    internal void ApplyCompletion(WorkOrder order)
    {
        if (order.Type != WorkOrderType.Preventive || order.MaintenancePlanId is not { } planId ||
            order.CompletedAt is not { } completedAt || plans.GetById(planId) is not { } plan) return;

        var history = workOrders.Query(w => w.MaintenancePlanId == planId);
        var latest = history.Where(w => w.Status != WorkOrderStatus.Cancelled)
            .OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.OrderNo, StringComparer.Ordinal)
            .FirstOrDefault();
        if (latest?.Id != order.Id) return;

        var completedDate = DateOnly.FromDateTime(time.ToLocal(completedAt));
        plan.LastCompletedDate = completedDate;
        var used = UsedKeys(history);
        if (plan.Cycle == MaintenanceCycle.ByUsage)
        {
            if (plan.NextDueUsage is { } threshold && plan.UsageInterval is > 0)
                plan.NextDueUsage = MaintenanceRules.NextUsageAfter(threshold,
                    assets.GetById(plan.AssetId)?.CurrentUsage, plan.UsageInterval.Value, used);
        }
        else
        {
            plan.NextDueDate = MaintenanceRules.NextDateAfter(plan.Cycle, completedDate, used);
        }
        plans.Update(plan);
    }

    /// <summary>
    /// 取消本期保養工單視為略過本期：計畫推進到下一期，否則同一週期不可重複產生會使計畫停滯。
    /// 呼叫端需持有 IBusinessLock。
    /// </summary>
    internal void SkipCycle(WorkOrder order)
    {
        if (order.Type != WorkOrderType.Preventive || order.MaintenancePlanId is not { } planId ||
            plans.GetById(planId) is not { } plan ||
            order.MaintenanceCycleKey != MaintenanceRules.CurrentCycleKey(plan)) return;

        var used = UsedKeys(workOrders.Query(w => w.MaintenancePlanId == planId));
        if (plan.Cycle == MaintenanceCycle.ByUsage)
        {
            if (plan.UsageInterval is not > 0) return;
            plan.NextDueUsage = MaintenanceRules.NextUsageAfter(plan.NextDueUsage!.Value,
                assets.GetById(plan.AssetId)?.CurrentUsage, plan.UsageInterval.Value, used);
        }
        else
        {
            var due = plan.NextDueDate!.Value;
            var from = due > time.Today ? due : time.Today;
            plan.NextDueDate = MaintenanceRules.NextDateAfter(plan.Cycle, from, used);
        }
        plans.Update(plan);
    }

    /// <summary>尚未完工的狀態；待驗收視為本期已完成。</summary>
    internal static bool IsPending(WorkOrderStatus status) =>
        WorkOrderStateMachine.IsActive(status) && status != WorkOrderStatus.Completed;

    private static HashSet<string> UsedKeys(IEnumerable<WorkOrder> history) =>
        history.Select(w => w.MaintenanceCycleKey).OfType<string>().ToHashSet();

    private WorkOrder CreateOrder(MaintenancePlan plan, Asset asset, string key)
    {
        var now = time.UtcNow;
        // 完工時限 = 保養日當天結束（台北）；依使用量沒有保養日，以預設提前天數推算。
        var dueDate = plan.Cycle != MaintenanceCycle.ByUsage && plan.NextDueDate is { } date
            ? date
            : time.Today.AddDays(settings.GetNonNegativeInt(SettingKeys.MaintenanceDefaultLeadDays, 7));
        var prefix = WorkOrderRules.OrderNoPrefix(WorkOrderType.Preventive, time.Today);
        var technician = plan.AssigneeUserId is { } id &&
                         users.GetById(id) is { IsActive: true, Role: UserRole.Technician } user
            ? user
            : null;

        var order = workOrders.Add(new WorkOrder
        {
            OrderNo = WorkOrderRules.NextOrderNo(prefix, workOrders.GetAll().Select(w => w.OrderNo)),
            Type = WorkOrderType.Preventive,
            Status = technician is null ? WorkOrderStatus.Open : WorkOrderStatus.Assigned,
            Priority = plan.Priority,
            AssetId = asset.Id,
            Title = plan.Name,
            Description = "系統依保養計畫自動產生。",
            AssigneeUserId = technician?.Id,
            RespondedAt = technician is null ? null : now,
            ResolveDueAt = time.ToUtc(dueDate.ToDateTime(new TimeOnly(23, 59, 59))),
            MaintenancePlanId = plan.Id,
            MaintenanceCycleKey = key,
            Checklist = plan.ChecklistItems.Select(text => new ChecklistItem { Text = text }).ToList()
        });

        AddLog(order, WorkOrderAction.Created, null, WorkOrderStatus.Open, null, now);
        if (technician is not null)
            AddLog(order, WorkOrderAction.Assigned, WorkOrderStatus.Open, WorkOrderStatus.Assigned,
                $"依保養計畫指派給 {technician.DisplayName}", now.AddTicks(1));
        audit.Record(AuditActions.GenerateMaintenance, nameof(WorkOrder), order.Id, order.OrderNo,
            $"保養計畫「{plan.Name}」週期 {key}", actorName: SystemActor);
        return order;
    }

    private void AddLog(WorkOrder order, WorkOrderAction action, WorkOrderStatus? from, WorkOrderStatus to,
        string? comment, DateTime at) =>
        logs.Add(new WorkOrderLog
        {
            WorkOrderId = order.Id,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            Comment = comment,
            OccurredAt = at
        });
}
