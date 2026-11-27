using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class DashboardService(
    IRepository<WorkOrder> workOrders,
    IRepository<Asset> assets,
    IRepository<Part> parts,
    ICurrentUser currentUser,
    ITimeProvider time) : IDashboardService
{
    public HomeSummary GetHomeSummary()
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException();
        var now = time.UtcNow;
        var all = workOrders.GetAll();

        bool Overdue(WorkOrder w) => WorkOrderStateMachine.IsActive(w.Status) &&
            (SlaRules.IsOverdue(w.ResponseDueAt, w.RespondedAt, now) || SlaRules.IsOverdue(w.ResolveDueAt, w.CompletedAt, now));
        bool Working(WorkOrder w) =>
            w.Status is WorkOrderStatus.Assigned or WorkOrderStatus.InProgress or WorkOrderStatus.WaitingParts;

        List<SummaryCard> cards;
        switch (currentUser.Role)
        {
            case UserRole.Requester:
                var mine = all.Where(w => w.RequesterUserId == userId).ToList();
                cards =
                [
                    new("處理中的報修", mine.Count(w => w.Status == WorkOrderStatus.Open || Working(w)), "bi-tools", "primary"),
                    new("待我驗收", mine.Count(w => w.Status == WorkOrderStatus.Completed), "bi-clipboard-check", "warning"),
                    new("已結案", mine.Count(w => w.Status == WorkOrderStatus.Closed), "bi-check-circle", "success")
                ];
                break;
            case UserRole.Technician:
                var assigned = all.Where(w => w.AssigneeUserId == userId).ToList();
                cards =
                [
                    new("我的待辦", assigned.Count(Working), "bi-list-task", "primary"),
                    new("可接單", all.Count(w => w.Status == WorkOrderStatus.Open), "bi-hand-index", "info"),
                    new("我的逾時工單", assigned.Count(Overdue), "bi-alarm", "danger")
                ];
                break;
            case UserRole.Supervisor:
            case UserRole.Admin:
                cards =
                [
                    new("待派工", all.Count(w => w.Status == WorkOrderStatus.Open), "bi-inbox", "info"),
                    new("進行中工單", all.Count(Working), "bi-tools", "primary"),
                    new("逾時工單", all.Count(Overdue), "bi-alarm", "danger"),
                    new("維修中資產", assets.GetAll().Count(a => a.Status == AssetStatus.UnderRepair), "bi-pc-display", "warning"),
                    new("低於安全庫存", parts.GetAll().Count(p => p.IsActive && p.StockQuantity < p.SafetyStock), "bi-box-seam", "danger")
                ];
                break;
            default:
                throw new ForbiddenException();
        }

        return new HomeSummary(cards);
    }
}
