using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Rules;

namespace AssetCare.Infrastructure.Json.Seeding;

/// <summary>檢查範例資料的參照完整性與商業規則一致性，回傳所有錯誤訊息。</summary>
public class SeedValidator(ITimeProvider time)
{
    private readonly List<string> _errors = [];

    public IReadOnlyList<string> Validate(SeedDataSet data)
    {
        _errors.Clear();
        CheckIdentity(data);
        CheckReferences(data);
        // 參照錯誤會讓後續檢查失去意義。
        if (_errors.Count > 0) return _errors.ToList();
        CheckAssets(data);
        CheckWorkOrders(data);
        CheckStock(data);
        CheckMaintenance(data);
        CheckAttachments(data);
        return _errors.ToList();
    }

    private void CheckIdentity(SeedDataSet data)
    {
        Unique("Id", data.All, e => e.Id.ToString());
        Unique("使用者帳號", data.Set<User>(), u => u.UserName.ToLowerInvariant());
        Unique("部門代碼", data.Set<Department>(), d => d.Code);
        Unique("地點代碼", data.Set<Location>(), l => l.Code);
        Unique("類別代碼", data.Set<AssetCategory>(), c => c.Code);
        Unique("資產編號", data.Set<Asset>(), a => a.AssetNo);
        Unique("工單單號", data.Set<WorkOrder>(), w => w.OrderNo);
        Unique("零件料號", data.Set<Part>(), p => p.PartNo);
        Unique("設定鍵值", data.Set<AppSetting>(), s => s.Key);
        foreach (var e in data.All.Where(e => e.Id == Guid.Empty || e.Version < 1))
            _errors.Add($"{e.GetType().Name} {e.Id}：Id 不可為空且 Version 需大於 0");
        var limit = time.UtcNow.AddMinutes(1);
        foreach (var e in data.All.Where(e => e.CreatedAt > limit || e.UpdatedAt > limit))
            _errors.Add($"{e.GetType().Name} {e.Id}：建立或更新時間晚於現在");
    }

    private void CheckReferences(SeedDataSet d)
    {
        Ref<Department, User>(d, x => x.ManagerUserId);
        Ref<User, Department>(d, x => x.DepartmentId);
        Ref<Location, Location>(d, x => x.ParentId);
        Ref<Asset, AssetCategory>(d, x => x.CategoryId);
        Ref<Asset, Location>(d, x => x.LocationId);
        Ref<Asset, Department>(d, x => x.DepartmentId);
        Ref<Asset, User>(d, x => x.CustodianUserId);
        Ref<Asset, Vendor>(d, x => x.VendorId);
        Ref<AssetTransfer, Asset>(d, x => x.AssetId);
        Ref<AssetTransfer, Location>(d, x => x.FromLocationId);
        Ref<AssetTransfer, Location>(d, x => x.ToLocationId);
        Ref<AssetTransfer, Department>(d, x => x.FromDepartmentId);
        Ref<AssetTransfer, Department>(d, x => x.ToDepartmentId);
        Ref<AssetTransfer, User>(d, x => x.FromCustodianUserId);
        Ref<AssetTransfer, User>(d, x => x.ToCustodianUserId);
        Ref<AssetTransfer, User>(d, x => x.TransferredByUserId);
        Ref<WorkOrder, Asset>(d, x => x.AssetId);
        Ref<WorkOrder, User>(d, x => x.RequesterUserId);
        Ref<WorkOrder, User>(d, x => x.AssigneeUserId);
        Ref<WorkOrder, MaintenancePlan>(d, x => x.MaintenancePlanId);
        Ref<WorkOrder, Vendor>(d, x => x.VendorId);
        Ref<WorkOrderLog, WorkOrder>(d, x => x.WorkOrderId);
        Ref<WorkOrderLog, User>(d, x => x.ActorUserId);
        Ref<WorkOrderPart, WorkOrder>(d, x => x.WorkOrderId);
        Ref<WorkOrderPart, Part>(d, x => x.PartId);
        Ref<WorkOrderPart, User>(d, x => x.IssuedByUserId);
        Ref<MaintenancePlan, Asset>(d, x => x.AssetId);
        Ref<MaintenancePlan, User>(d, x => x.AssigneeUserId);
        Ref<Part, Vendor>(d, x => x.VendorId);
        Ref<PartTransaction, Part>(d, x => x.PartId);
        Ref<PartTransaction, WorkOrder>(d, x => x.WorkOrderId);
        Ref<PartTransaction, User>(d, x => x.OperatorUserId);
        Ref<Notification, User>(d, x => x.UserId);
        Ref<EmailLog, User>(d, x => x.ToUserId);
        Ref<EmailLog, Notification>(d, x => x.NotificationId);
        Ref<Attachment, User>(d, x => x.UploadedByUserId);
        Ref<AuditLog, User>(d, x => x.UserId);
    }

    private void CheckAssets(SeedDataSet data)
    {
        var orders = data.Set<WorkOrder>().ToLookup(w => w.AssetId);
        foreach (var asset in data.Set<Asset>())
        {
            var activeRepair = orders[asset.Id].Any(w =>
                w.Type == WorkOrderType.Repair && WorkOrderStateMachine.IsActive(w.Status));
            if (asset.Status == AssetStatus.Scrapped)
            {
                if (orders[asset.Id].Any(w => WorkOrderStateMachine.IsActive(w.Status)))
                    _errors.Add($"{asset.AssetNo}：已報廢資產不可有未結案工單");
                if (asset.ScrappedAt is null || string.IsNullOrWhiteSpace(asset.ScrapReason))
                    _errors.Add($"{asset.AssetNo}：已報廢資產需有報廢時間與原因");
                else if (orders[asset.Id].Any(w => w.CreatedAt > asset.ScrappedAt))
                    _errors.Add($"{asset.AssetNo}：報廢後不可再開工單");
            }
            else if (activeRepair != (asset.Status == AssetStatus.UnderRepair))
            {
                _errors.Add($"{asset.AssetNo}：資產狀態 {asset.Status} 與未結案報修工單不一致");
            }

            if (asset.SalvageValue < 0 || asset.SalvageValue > asset.PurchaseCost || asset.UsefulLifeYears <= 0)
                _errors.Add($"{asset.AssetNo}：折舊參數不合理");

            var latest = data.Set<AssetTransfer>().Where(t => t.AssetId == asset.Id).MaxBy(t => t.TransferredAt);
            if (latest is not null && (latest.ToLocationId != asset.LocationId ||
                                       latest.ToCustodianUserId != asset.CustodianUserId))
                _errors.Add($"{asset.AssetNo}：最新轉移紀錄與目前位置或保管人不一致");
        }
    }

    private void CheckWorkOrders(SeedDataSet data)
    {
        var users = data.Set<User>().ToDictionary(u => u.Id);
        var logs = data.Set<WorkOrderLog>().ToLookup(l => l.WorkOrderId);
        var now = time.UtcNow;

        foreach (var order in data.Set<WorkOrder>())
        {
            void Fail(string message) => _errors.Add($"{order.OrderNo}：{message}");

            var history = logs[order.Id].OrderBy(l => l.OccurredAt).ToList();
            if (history.Count == 0 || history[0].Action != WorkOrderAction.Created ||
                history[0].FromStatus is not null || history[0].ToStatus != WorkOrderStatus.Open)
            {
                Fail("處理歷程必須以建立工單開始");
                continue;
            }

            var status = WorkOrderStatus.Open;
            foreach (var log in history.Skip(1))
            {
                if (log.FromStatus is null && log.ToStatus is null) continue;
                if (log.FromStatus != status || log.ToStatus is not { } to ||
                    !WorkOrderStateMachine.CanTransition(status, to))
                {
                    Fail($"狀態歷程順序不合法（目前 {status}，紀錄 {log.FromStatus}→{log.ToStatus}）");
                    break;
                }
                status = to;
            }
            if (status != order.Status) Fail($"狀態 {order.Status} 與歷程最後狀態 {status} 不一致");
            if (history.Any(l => l.OccurredAt > now.AddMinutes(1))) Fail("歷程時間晚於現在");

            var needsAssignee = order.Status is not (WorkOrderStatus.Open or WorkOrderStatus.Cancelled);
            if (needsAssignee && (order.AssigneeUserId is null || order.RespondedAt is null)) Fail("缺少負責技術員或回應時間");
            if (order.AssigneeUserId is { } assignee && users[assignee].Role != UserRole.Technician) Fail("負責人必須是技術員");
            if (order.Status is WorkOrderStatus.Completed or WorkOrderStatus.Closed != order.CompletedAt is not null)
                Fail("完工時間與狀態不一致");
            if (order.Status == WorkOrderStatus.Closed != order.ClosedAt is not null) Fail("結案時間與狀態不一致");
            if (order.Status == WorkOrderStatus.Cancelled != order.CancelledAt is not null) Fail("取消時間與狀態不一致");
            if (order.Type == WorkOrderType.Repair && order.RequesterUserId is null) Fail("報修工單需有申請人");

            if (order.Status != WorkOrderStatus.Cancelled)
            {
                // 產生後時間持續前進，只檢查「已標示逾時者確實逾時」。
                if (order.IsResponseOverdue && !SlaRules.IsOverdue(order.ResponseDueAt, order.RespondedAt, now))
                    Fail("標示回應逾時但實際未逾時");
                if (order.IsResolveOverdue && !SlaRules.IsOverdue(order.ResolveDueAt, order.CompletedAt, now))
                    Fail("標示完工逾時但實際未逾時");
            }
        }
    }

    private void CheckStock(SeedDataSet data)
    {
        var transactions = data.Set<PartTransaction>().ToLookup(t => t.PartId);
        foreach (var part in data.Set<Part>())
        {
            if (part.StockQuantity < 0) _errors.Add($"{part.PartNo}：庫存為負");
            int? balance = null;
            foreach (var t in transactions[part.Id].OrderBy(t => t.OccurredAt))
            {
                if (t.BalanceAfter < 0 || t.BalanceAfter - t.Quantity < 0 ||
                    (balance is { } previous && previous + t.Quantity != t.BalanceAfter))
                    _errors.Add($"{part.PartNo}：異動結餘不連續或為負");
                balance = t.BalanceAfter;
            }
            if (balance is { } final && final != part.StockQuantity)
                _errors.Add($"{part.PartNo}：目前庫存與最後一筆異動結餘不一致");
        }

        var issued = data.Set<PartTransaction>().Where(t => t.Type == PartTransactionType.Issue)
            .Select(t => (t.WorkOrderId, t.PartId, Quantity: -t.Quantity)).Order().ToList();
        var used = data.Set<WorkOrderPart>()
            .Select(p => (WorkOrderId: (Guid?)p.WorkOrderId, p.PartId, p.Quantity)).Order().ToList();
        if (!issued.SequenceEqual(used)) _errors.Add("工單使用零件與領用異動紀錄不一致");
    }

    private void CheckMaintenance(SeedDataSet data)
    {
        var plans = data.Set<MaintenancePlan>().ToDictionary(p => p.Id);
        var preventive = data.Set<WorkOrder>().Where(w => w.Type == WorkOrderType.Preventive).ToList();
        foreach (var order in preventive)
        {
            if (order.MaintenancePlanId is not { } planId || string.IsNullOrEmpty(order.MaintenanceCycleKey))
                _errors.Add($"{order.OrderNo}：保養工單需指定保養計畫與週期");
            else if (plans[planId].AssetId != order.AssetId)
                _errors.Add($"{order.OrderNo}：資產與保養計畫不符");
        }
        Unique("保養週期工單", preventive, w => $"{w.MaintenancePlanId}|{w.MaintenanceCycleKey}");

        foreach (var plan in data.Set<MaintenancePlan>())
        {
            var byUsage = plan.Cycle == MaintenanceCycle.ByUsage;
            if (byUsage ? plan.UsageInterval is null || plan.NextDueUsage is null : plan.NextDueDate is null)
                _errors.Add($"保養計畫「{plan.Name}」缺少下次保養日或使用量條件");
        }
    }

    private void CheckAttachments(SeedDataSet data)
    {
        var assets = data.Set<Asset>().Select(a => a.Id).ToHashSet();
        var orders = data.Set<WorkOrder>().Select(w => w.Id).ToHashSet();
        foreach (var a in data.Set<Attachment>())
        {
            var owners = a.OwnerType == AttachmentOwnerType.Asset ? assets : orders;
            if (!owners.Contains(a.OwnerId)) _errors.Add($"附件 {a.FileName}：找不到所屬{a.OwnerType}");
            if (!data.Uploads.ContainsKey(a.StoredFileName)) _errors.Add($"附件 {a.FileName}：缺少實體檔 {a.StoredFileName}");
        }
    }

    private void Unique<T>(string label, IEnumerable<T> items, Func<T, string> key)
    {
        foreach (var group in items.GroupBy(key).Where(g => g.Count() > 1))
            _errors.Add($"{label}重複：{group.Key}");
    }

    private void Ref<TSource, TTarget>(SeedDataSet data, Func<TSource, Guid?> selector)
        where TSource : BaseEntity where TTarget : BaseEntity
    {
        var ids = data.Set<TTarget>().Select(t => t.Id).ToHashSet();
        foreach (var item in data.Set<TSource>())
            if (selector(item) is { } id && !ids.Contains(id))
                _errors.Add($"{typeof(TSource).Name} {item.Id}：參照的 {typeof(TTarget).Name} {id} 不存在");
    }
}
