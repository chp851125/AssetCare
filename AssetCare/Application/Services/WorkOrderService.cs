using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class WorkOrderService(
    IRepository<WorkOrder> workOrders,
    IRepository<WorkOrderLog> logs,
    IRepository<WorkOrderPart> orderParts,
    IRepository<Asset> assets,
    IRepository<AssetCategory> categories,
    IRepository<Location> locations,
    IRepository<User> users,
    IRepository<Part> parts,
    IRepository<Vendor> vendors,
    IRepository<Attachment> attachments,
    IRepository<AppSetting> settings,
    ICurrentUser currentUser,
    ITimeProvider time) : IWorkOrderService
{
    private const int PageSize = 10;

    public WorkOrderList Search(WorkOrderQuery query)
    {
        var userId = currentUser.RequireUserId();
        var scopes = AvailableScopes();
        // 各角色的第一個分頁即預設分頁。
        var scope = query.Scope is { } requested && scopes.Contains(requested) ? requested : scopes[0];
        var now = time.UtcNow;

        var assetMap = assets.GetAll().ToDictionary(a => a.Id);
        var hierarchy = new LocationHierarchy(locations.GetAll());
        var userNames = UserNames();

        IEnumerable<WorkOrder> source = workOrders.GetAll();
        source = scope switch
        {
            WorkOrderScope.Mine => source.Where(w => w.RequesterUserId == userId),
            WorkOrderScope.Open => source.Where(w => w.Status == WorkOrderStatus.Open),
            WorkOrderScope.Todo => source.Where(w => w.AssigneeUserId == userId && w.Status is
                WorkOrderStatus.Assigned or WorkOrderStatus.InProgress or WorkOrderStatus.WaitingParts),
            _ => source
        };
        if (query.Status is { } status) source = source.Where(w => w.Status == status);
        if (query.Priority is { } priority) source = source.Where(w => w.Priority == priority);
        if (query.Type is { } type) source = source.Where(w => w.Type == type);
        if (query.OverdueOnly)
            source = source.Where(w => WorkOrderStateMachine.IsActive(w.Status) &&
                (WorkOrderRules.IsResponseOverdue(w, now) || WorkOrderRules.IsResolveOverdue(w, now)));
        if (query.Keyword?.Trim() is { Length: > 0 } keyword)
            source = source.Where(w =>
            {
                var asset = assetMap.GetValueOrDefault(w.AssetId);
                return new[] { w.OrderNo, w.Title, w.Description, asset?.AssetNo, asset?.Name }
                    .Any(v => v?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true);
            });

        // 待處理清單依急迫程度排列，其他依建立時間由新到舊；我的報修把待驗收排在最前。
        var ordered = scope switch
        {
            WorkOrderScope.Open or WorkOrderScope.Todo =>
                source.OrderBy(w => w.ResolveDueAt).ThenByDescending(w => w.Priority),
            WorkOrderScope.Mine => source.OrderByDescending(w => w.Status == WorkOrderStatus.Completed)
                .ThenByDescending(w => w.CreatedAt),
            _ => source.OrderByDescending(w => w.CreatedAt)
        };

        var items = ordered.Select(w =>
        {
            var asset = assetMap.GetValueOrDefault(w.AssetId);
            return new WorkOrderListItem(w.Id, w.Version, w.OrderNo, w.Type, w.Status, w.Priority, w.Title,
                asset?.AssetNo ?? "—", asset?.Name ?? "—",
                asset is null ? "—" : hierarchy.NameOf(asset.LocationId),
                Name(userNames, w.RequesterUserId), Name(userNames, w.AssigneeUserId), w.CreatedAt, w.ResolveDueAt,
                WorkOrderRules.IsResponseOverdue(w, now), WorkOrderRules.IsResolveOverdue(w, now),
                CanAccept(w), CanProcess(w, WorkOrderAction.Started));
        }).ToList();

        return new WorkOrderList(scope, scopes, PagedResult<WorkOrderListItem>.From(items, query.Page, PageSize));
    }

    public WorkOrderDetail GetDetail(Guid id)
    {
        var userId = currentUser.RequireUserId();
        var order = Find(id);
        var now = time.UtcNow;
        var days = DuplicateDays();
        var full = WorkOrderAccess.CanView(order, currentUser);
        // 近期報修可供他人確認是否已有人通報（僅摘要）。
        if (!full && !WorkOrderRules.IsRecentRepair(order, now, days))
            throw new ForbiddenException("只能檢視自己的報修工單。");

        var asset = assets.GetById(order.AssetId) ?? throw new NotFoundException("找不到資產。");
        var userMap = users.GetAll().ToDictionary(u => u.Id);
        var userNames = userMap.Values.ToDictionary(u => u.Id, u => u.DisplayName);
        var requester = order.RequesterUserId is { } r ? userMap.GetValueOrDefault(r) : null;

        var active = WorkOrderStateMachine.IsActive(order.Status);
        var outside = order.VendorId is not null && order.ReturnedDate is null;
        var assigned = order.AssigneeUserId is not null;
        var can = new WorkOrderPermissions(
            Assign: currentUser.IsManager() && Allows(order, WorkOrderAction.Assigned),
            Reassign: currentUser.IsManager() && Allows(order, WorkOrderAction.Reassigned),
            Accept: CanAccept(order),
            Start: CanProcess(order, WorkOrderAction.Started),
            Wait: CanProcess(order, WorkOrderAction.WaitingParts),
            Resume: CanProcess(order, WorkOrderAction.Resumed),
            Complete: CanProcess(order, WorkOrderAction.Completed) && !outside,
            Verify: WorkOrderAccess.CanVerify(order, currentUser) && Allows(order, WorkOrderAction.Verified),
            Cancel: WorkOrderAccess.CanVerify(order, currentUser) && Allows(order, WorkOrderAction.Cancelled) && !outside,
            IssuePart: CanProcess(order, WorkOrderAction.PartIssued),
            SendOut: assigned && CanProcess(order, WorkOrderAction.SentToVendor) && !outside,
            ReturnFromVendor: CanProcess(order, WorkOrderAction.ReturnedFromVendor) && outside,
            Upload: active && (currentUser.IsManager() || order.AssigneeUserId == userId ||
                               order.RequesterUserId == userId),
            Checklist: order.Checklist.Count > 0 && CanProcess(order, WorkOrderAction.ChecklistUpdated));

        VendorRepairInfo? vendorInfo = null;
        if (full && order.VendorId is { } vendorId)
        {
            var vendor = vendors.GetById(vendorId);
            vendorInfo = new VendorRepairInfo(vendor?.Name ?? "—", vendor?.Phone, order.SentOutDate,
                order.ExpectedReturnDate, order.ReturnedDate, currentUser.IsStaff() ? order.VendorCost : null);
        }

        List<WorkOrderLogItem> history = !full ? [] : logs.Query(l => l.WorkOrderId == id)
            .OrderByDescending(l => l.OccurredAt).ThenByDescending(l => l.CreatedAt)
            .Select(l => new WorkOrderLogItem(l.Action, l.FromStatus, l.ToStatus, l.Comment,
                Name(userNames, l.ActorUserId) ?? "系統", l.OccurredAt))
            .ToList();

        var partMap = parts.GetAll().ToDictionary(p => p.Id);
        List<WorkOrderPartItem> used = !full ? [] : orderParts.Query(p => p.WorkOrderId == id)
            .OrderBy(p => p.IssuedAt)
            .Select(p =>
            {
                var part = partMap.GetValueOrDefault(p.PartId);
                return new WorkOrderPartItem(p.PartId, part?.PartNo ?? "—", part?.Name ?? "—", part?.Unit ?? "",
                    p.Quantity, p.UnitCost, Name(userNames, p.IssuedByUserId) ?? "—", p.IssuedAt);
            }).ToList();

        List<AttachmentItem> photos = !full ? [] : attachments
            .Query(a => a.OwnerType == AttachmentOwnerType.WorkOrder && a.OwnerId == id)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AttachmentItem(a.Id, a.FileName, a.Caption, a.ContentType, a.SizeBytes, a.CreatedAt,
                Name(userNames, a.UploadedByUserId) ?? "—",
                currentUser.IsManager() || (active && a.UploadedByUserId == userId)))
            .ToList();

        List<Option> technicians = !can.Assign && !can.Reassign ? [] : TechnicianOptions(order.AssigneeUserId);
        List<PartOption> partOptions = !can.IssuePart ? [] : ActivePartOptions();
        List<Option> vendorOptions = !can.SendOut ? [] : vendors.Query(v => v.IsActive).OrderBy(v => v.Name)
            .Select(v => new Option(v.Id, v.Name)).ToList();

        return new WorkOrderDetail(order.Id, order.Version, order.OrderNo, order.Type, order.Status, order.Priority,
            order.Title, order.Description, ToReportAsset(asset), requester?.DisplayName,
            full ? requester?.Phone : null, Name(userNames, order.AssigneeUserId),
            order.CreatedAt, order.ResponseDueAt, order.RespondedAt, WorkOrderRules.IsResponseOverdue(order, now),
            order.ResolveDueAt, order.CompletedAt, WorkOrderRules.IsResolveOverdue(order, now),
            order.StartedAt, order.ClosedAt, order.CancelledAt, order.CancelReason,
            full ? order.FaultCause : null, full ? order.Resolution : null, order.LaborHours, order.ReworkCount,
            full ? order.Checklist : [], !full, vendorInfo, history, used, photos,
            RecentRepairs(asset.Id, order.Id, now, days, userNames), days, can, technicians, partOptions,
            vendorOptions, time.Today, order.MaintenancePlanId);
    }

    public IReadOnlyList<Option> GetReportableAssets()
    {
        currentUser.RequireUserId();
        return assets.Query(a => a.Status != AssetStatus.Scrapped)
            .OrderBy(a => a.AssetNo, StringComparer.Ordinal)
            .Select(a => new Option(a.Id, $"{a.AssetNo} {a.Name}")).ToList();
    }

    public ReportContext GetReportContext(Guid assetId)
    {
        currentUser.RequireUserId();
        var asset = assets.GetById(assetId) ?? throw new NotFoundException("找不到資產。");
        AssetRules.EnsureNotScrapped(asset, "開立工單");
        var days = DuplicateDays();
        return new ReportContext(ToReportAsset(asset),
            RecentRepairs(asset.Id, null, time.UtcNow, days, UserNames()), days);
    }

    public CompleteContext GetCompleteContext(Guid id)
    {
        currentUser.RequireStaff();
        var order = Find(id);
        if (!WorkOrderAccess.CanProcess(order, currentUser))
            throw new ForbiddenException("只能處理指派給自己的工單。");
        WorkOrderWorkflowService.EnsureAllowed(order, WorkOrderAction.Completed);
        return new CompleteContext(order.Id, order.Version, order.OrderNo, order.Title,
            assets.GetById(order.AssetId)?.Name ?? "—", order.FaultCause, order.Resolution, order.LaborHours,
            ActivePartOptions(), order.Type, order.Checklist);
    }

    private List<WorkOrderScope> AvailableScopes() => currentUser.Role switch
    {
        UserRole.Requester => [WorkOrderScope.Mine],
        UserRole.Technician => [WorkOrderScope.Todo, WorkOrderScope.Open, WorkOrderScope.Mine, WorkOrderScope.All],
        _ => [WorkOrderScope.Open, WorkOrderScope.All, WorkOrderScope.Mine]
    };

    private static bool Allows(WorkOrder order, WorkOrderAction action) =>
        WorkOrderStateMachine.Allows(action, order.Status);

    private bool CanAccept(WorkOrder order) =>
        currentUser.Role == UserRole.Technician && Allows(order, WorkOrderAction.Accepted);

    private bool CanProcess(WorkOrder order, WorkOrderAction action) =>
        WorkOrderAccess.CanProcess(order, currentUser) && Allows(order, action);

    private List<Option> TechnicianOptions(Guid? current)
    {
        // 附上目前未結案件數，協助主管分配工作量。
        var load = workOrders.GetAll()
            .Where(w => w.AssigneeUserId != null && WorkOrderStateMachine.IsActive(w.Status))
            .CountBy(w => w.AssigneeUserId!.Value).ToDictionary();
        return users.Query(u => u.IsActive && u.Role == UserRole.Technician)
            .Where(u => u.Id != current)
            .OrderBy(u => load.GetValueOrDefault(u.Id)).ThenBy(u => u.UserName)
            .Select(u => new Option(u.Id, $"{u.DisplayName}（進行中 {load.GetValueOrDefault(u.Id)} 件）"))
            .ToList();
    }

    private List<PartOption> ActivePartOptions() => parts.Query(p => p.IsActive)
        .OrderBy(p => p.Name)
        .Select(p => new PartOption(p.Id, $"{p.Name}（庫存 {p.StockQuantity} {p.Unit}）", p.StockQuantity))
        .ToList();

    private List<RelatedWorkOrder> RecentRepairs(Guid assetId, Guid? exceptId, DateTime now, int days,
        Dictionary<Guid, string> userNames) =>
        workOrders.Query(w => w.AssetId == assetId)
            .Where(w => w.Id != exceptId && WorkOrderRules.IsRecentRepair(w, now, days))
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new RelatedWorkOrder(w.Id, w.OrderNo, w.Title, w.Status, w.CreatedAt,
                Name(userNames, w.RequesterUserId)))
            .ToList();

    private ReportAsset ToReportAsset(Asset asset) => new(asset.Id, asset.AssetNo, asset.Name,
        categories.GetById(asset.CategoryId)?.Name ?? "—", asset.Status,
        new LocationHierarchy(locations.GetAll()).PathOf(asset.LocationId),
        asset.CustodianUserId is { } u ? users.GetById(u)?.DisplayName : null);

    private int DuplicateDays() => settings.GetInt(SettingKeys.DuplicateReportDays, 7);

    private Dictionary<Guid, string> UserNames() => users.GetAll().ToDictionary(u => u.Id, u => u.DisplayName);

    private static string? Name(Dictionary<Guid, string> names, Guid? id) =>
        id is { } value ? names.GetValueOrDefault(value) : null;

    private WorkOrder Find(Guid id) => workOrders.GetById(id) ?? throw new NotFoundException("找不到工單。");
}
