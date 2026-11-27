using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class ReminderService(
    IRepository<Asset> assets,
    IRepository<MaintenancePlan> plans,
    IRepository<Notification> sent,
    IRepository<AppSetting> settings,
    INotificationDispatcher notifications,
    IBusinessLock businessLock,
    ITimeProvider time) : IReminderService
{
    public void SendDueReminders() => businessLock.Run(() =>
    {
        var today = time.Today;
        var managers = notifications.ManagerIds().Select(id => (Guid?)id).ToList();
        var noticeDays = settings.GetInt(SettingKeys.WarrantyNoticeDays, 30);

        foreach (var asset in assets.Query(a => a.Status != AssetStatus.Scrapped))
        {
            if (WarrantyRules.StateOf(asset.WarrantyEndDate, today, noticeDays) != WarrantyState.Expiring) continue;
            SendOnce(managers.Append(asset.CustodianUserId), NotificationType.WarrantyExpiring,
                $"保固即將到期（{asset.WarrantyEndDate:yyyy/MM/dd}）：{asset.Name}",
                $"{asset.AssetNo} 保固即將到期，請確認是否續約。", $"/Assets/Details/{asset.Id}");
        }

        foreach (var plan in plans.Query(p => p.IsActive && p.NextDueDate != null))
        {
            var due = plan.NextDueDate!.Value;
            // 已逾期者提醒逾期；尚未到期者進入提前產生期間才提醒。
            if (due < today)
            {
                SendOnce(managers.Append(plan.AssigneeUserId), NotificationType.MaintenanceDue,
                    $"保養已逾期（{due:yyyy/MM/dd}）：{plan.Name}",
                    $"{assets.GetById(plan.AssetId)?.Name} 原定 {due:yyyy/MM/dd} 保養，尚未完成。",
                    $"/MaintenancePlans/Details/{plan.Id}");
                continue;
            }
            if (due.DayNumber - today.DayNumber > plan.LeadDays) continue;
            SendOnce(managers.Append(plan.AssigneeUserId), NotificationType.MaintenanceDue,
                $"保養即將到期（{due:yyyy/MM/dd}）：{plan.Name}",
                $"{assets.GetById(plan.AssetId)?.Name} 預定 {due:yyyy/MM/dd} 保養。",
                $"/MaintenancePlans/Details/{plan.Id}");
        }
    });

    /// <summary>標題含到期日，作為「同一到期日只通知一次」的判斷依據。</summary>
    private void SendOnce(IEnumerable<Guid?> userIds, NotificationType type, string title, string message, string link)
    {
        var already = sent.Query(n => n.Type == type && n.LinkUrl == link && n.Title == title)
            .Select(n => n.UserId).ToHashSet();
        notifications.Send(userIds.Where(id => id is { } value && !already.Contains(value)), type, title, message, link);
    }
}
