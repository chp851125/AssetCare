using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class SlaService(
    IRepository<WorkOrder> workOrders,
    IRepository<Asset> assets,
    INotificationDispatcher notifications,
    IBusinessLock businessLock,
    ITimeProvider time) : ISlaService
{
    public int ScanOverdue()
    {
        var flagged = businessLock.Run(() =>
        {
            var now = time.UtcNow;
            var result = new List<(WorkOrder Order, string Reason)>();
            foreach (var order in workOrders.Query(w => w.Status != WorkOrderStatus.Cancelled))
            {
                var response = !order.IsResponseOverdue &&
                               SlaRules.IsOverdue(order.ResponseDueAt, order.RespondedAt, now);
                var resolve = !order.IsResolveOverdue &&
                              SlaRules.IsOverdue(order.ResolveDueAt, order.CompletedAt, now);
                if (!response && !resolve) continue;

                // 已結案的工單只補標示（供統計），不再通知。
                var notify = WorkOrderStateMachine.IsActive(order.Status);
                var reasons = new List<string>();
                if (response)
                {
                    order.IsResponseOverdue = true;
                    if (notify) order.ResponseOverdueNotifiedAt = now;
                    reasons.Add("超過回應時限");
                }
                if (resolve)
                {
                    order.IsResolveOverdue = true;
                    if (notify) order.ResolveOverdueNotifiedAt = now;
                    reasons.Add("超過完工時限");
                }
                workOrders.Update(order);
                result.Add((order, notify ? string.Join("、", reasons) : ""));
            }
            return result;
        });

        var managers = notifications.ManagerIds();
        foreach (var (order, reason) in flagged.Where(x => x.Reason.Length > 0))
            notifications.Send(managers.Select(id => (Guid?)id).Append(order.AssigneeUserId),
                NotificationType.SlaOverdue, $"工單 {order.OrderNo} 已逾時",
                $"{assets.GetById(order.AssetId)?.Name}「{order.Title}」{reason}。", WorkOrderLinks.Details(order.Id));
        return flagged.Count;
    }
}
