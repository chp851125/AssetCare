using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class WorkOrderWorkflowService(
    IRepository<WorkOrder> workOrders,
    IRepository<WorkOrderLog> logs,
    IRepository<Asset> assets,
    IRepository<User> users,
    IRepository<Vendor> vendors,
    IRepository<AppSetting> settings,
    PartService partService,
    AttachmentService attachmentService,
    MaintenanceScheduler maintenance,
    INotificationDispatcher notifications,
    IBusinessLock businessLock,
    ICurrentUser currentUser,
    IAuditService audit,
    ITimeProvider time) : IWorkOrderWorkflowService
{
    private const int MaxTitleLength = 40;

    public Guid Report(ReportInput input, IReadOnlyList<UploadedFile> photos)
    {
        var userId = currentUser.RequireUserId();
        var description = InputText.Required(input.Description, "故障描述", 1000);
        if (!Enum.IsDefined(input.Priority)) throw new BusinessRuleException("請選擇優先級。");
        // 照片全部檢查通過才開單。
        var accepted = attachmentService.CheckPhotos(photos);

        var (order, asset, duplicates) = businessLock.Run(() =>
        {
            var target = assets.GetById(input.AssetId) ?? throw new NotFoundException("找不到資產。");
            AssetRules.EnsureNotScrapped(target, "開立工單");

            var now = time.UtcNow;
            var days = settings.GetInt(SettingKeys.DuplicateReportDays, 7);
            var recent = workOrders.Query(w => w.AssetId == target.Id)
                .Where(w => WorkOrderRules.IsRecentRepair(w, now, days))
                .OrderByDescending(w => w.CreatedAt).Select(w => w.OrderNo).ToList();

            var (response, resolve) = SlaHours(input.Priority);
            var prefix = WorkOrderRules.OrderNoPrefix(WorkOrderType.Repair, time.Today);
            var created = workOrders.Add(new WorkOrder
            {
                OrderNo = WorkOrderRules.NextOrderNo(prefix, workOrders.GetAll().Select(w => w.OrderNo)),
                Type = WorkOrderType.Repair,
                Status = WorkOrderStatus.Open,
                Priority = input.Priority,
                AssetId = target.Id,
                Title = TitleFrom(description),
                Description = description,
                RequesterUserId = userId,
                ResponseDueAt = now.AddHours(response),
                ResolveDueAt = now.AddHours(resolve)
            });

            if (target.Status != AssetStatus.UnderRepair && AutoSetUnderRepair())
            {
                target.Status = AssetStatus.UnderRepair;
                assets.Update(target);
            }
            AddLog(created, WorkOrderAction.Created, null, WorkOrderStatus.Open, null);
            attachmentService.Save(AttachmentOwnerType.WorkOrder, created.Id, accepted, null);
            audit.Record(AuditActions.Create, nameof(WorkOrder), created.Id, created.OrderNo, $"報修 {target.AssetNo}");
            return (created, target, recent);
        });

        var note = duplicates.Count == 0 ? "" : $"（近期重複報修：{string.Join("、", duplicates)}）";
        notifications.Send(Managers(), NotificationType.NewRepair,
            $"新報修 {order.OrderNo}（{order.Priority.DisplayName()}）",
            $"{asset.Name}：{order.Title}{note}", WorkOrderLinks.Details(order.Id));
        return order.Id;
    }

    public void Assign(Guid id, int version, Guid technicianId, string? note)
    {
        currentUser.RequireManager();
        var text = InputText.Optional(note, "派工說明", 500);
        var (order, previous, technician) = businessLock.Run(() =>
        {
            var target = Find(id);
            var action = target.Status == WorkOrderStatus.Open ? WorkOrderAction.Assigned : WorkOrderAction.Reassigned;
            EnsureAllowed(target, action);
            var assignee = users.GetById(technicianId) is { IsActive: true, Role: UserRole.Technician } u
                ? u
                : throw new BusinessRuleException("請選擇啟用中的技術員。");
            var old = target.AssigneeUserId;
            if (old == assignee.Id) throw new BusinessRuleException("此工單已指派給該技術員。");

            target.AssigneeUserId = assignee.Id;
            target.RespondedAt ??= time.UtcNow;
            Transition(target, version, action, $"指派給 {assignee.DisplayName}" + (text is null ? "" : $"：{text}"));
            audit.Record(AuditActions.Assign, nameof(WorkOrder), target.Id, target.OrderNo,
                $"{action.DisplayName()}給 {assignee.DisplayName}");
            return (target, old, assignee);
        });

        Notify([technician.Id], NotificationType.WorkOrderAssigned, order, "派工通知", $"已指派給您處理。{text}");
        if (previous is null)
        {
            Notify([order.RequesterUserId], NotificationType.WorkOrderStatusChanged, order, "已派工",
                $"已指派 {technician.DisplayName} 處理。");
        }
        else
        {
            Notify([previous], NotificationType.WorkOrderStatusChanged, order, "已改派",
                $"已改派給 {technician.DisplayName}。");
        }
    }

    public void Accept(Guid id, int version)
    {
        var userId = currentUser.RequireUserId();
        if (currentUser.Role != UserRole.Technician) throw new ForbiddenException("只有技術員可以接單。");
        var order = businessLock.Run(() =>
        {
            var target = Find(id);
            EnsureAllowed(target, WorkOrderAction.Accepted);
            target.AssigneeUserId = userId;
            target.RespondedAt ??= time.UtcNow;
            Transition(target, version, WorkOrderAction.Accepted, null);
            return target;
        });
        Notify(Managers().Append(order.RequesterUserId), NotificationType.WorkOrderStatusChanged, order, "已接單",
            $"{users.GetById(userId)?.DisplayName} 已接單。");
    }

    public void Start(Guid id, int version)
    {
        var order = Process(id, version, WorkOrderAction.Started, null, target => target.StartedAt ??= time.UtcNow);
        Notify([order.RequesterUserId], NotificationType.WorkOrderStatusChanged, order, "開始處理", "技術員已開始處理。");
    }

    public void WaitForParts(Guid id, int version, string? reason)
    {
        var text = InputText.Required(reason, "待料原因", 500);
        var order = Process(id, version, WorkOrderAction.WaitingParts, text);
        Notify(Managers().Append(order.RequesterUserId), NotificationType.WorkOrderStatusChanged, order, "待料中", text);
    }

    public void Resume(Guid id, int version, string? note)
    {
        var order = Process(id, version, WorkOrderAction.Resumed, InputText.Optional(note, "說明", 500));
        Notify([order.RequesterUserId], NotificationType.WorkOrderStatusChanged, order, "恢復處理", "已恢復處理。");
    }

    public void Complete(Guid id, int version, CompleteInput input)
    {
        currentUser.RequireStaff();
        var cause = InputText.Required(input.FaultCause, "故障原因", 1000);
        var resolution = InputText.Required(input.Resolution, "處理方式", 1000);
        if (input.LaborHours is < 0 or > 1000) throw new BusinessRuleException("工時須介於 0 到 1000 小時。");
        var accepted = attachmentService.CheckPhotos(input.Photos);

        var (order, low) = businessLock.Run(() =>
        {
            var target = FindForProcessing(id, WorkOrderAction.Completed);
            EnsureNotOutside(target, "完工");
            var lines = partService.CheckAvailability(input.Parts);

            target.CompletedAt = time.UtcNow;
            target.FaultCause = cause;
            target.Resolution = resolution;
            target.LaborHours = Math.Round(input.LaborHours, 2);
            if (target.Checklist.Count > 0)
            {
                if (input.Checklist is { } entries) ApplyChecklist(target, entries);
                EnsureChecklistDone(target);
            }
            // 版本衝突時在此中止，不會扣庫存。
            Transition(target, version, WorkOrderAction.Completed, resolution);
            var crossed = partService.Consume(target, lines);
            LogParts(target, lines);
            attachmentService.Save(AttachmentOwnerType.WorkOrder, target.Id, accepted, "完工照片");
            maintenance.ApplyCompletion(target);
            return (target, crossed);
        });

        partService.NotifyLowStock(low);
        // 保養工單沒有申請人，由主管驗收。
        var verifiers = order.RequesterUserId is null ? Managers() : [order.RequesterUserId];
        Notify(verifiers, NotificationType.WorkOrderStatusChanged, order, "完工待驗收", "請確認處理結果並完成驗收。");
    }

    public void Verify(Guid id, int version, string? note)
    {
        var text = InputText.Optional(note, "驗收意見", 500);
        var order = Review(id, version, WorkOrderAction.Verified, text, target => target.ClosedAt = time.UtcNow);
        Notify([order.AssigneeUserId], NotificationType.VerificationResult, order, "驗收通過", text ?? "已結案。");
    }

    public void Reject(Guid id, int version, string? reason)
    {
        var text = InputText.Required(reason, "退回原因", 500);
        var order = Review(id, version, WorkOrderAction.Rejected, text, target =>
        {
            // 保留前次完工填寫內容供技術員參考。
            target.CompletedAt = null;
            target.ReworkCount++;
        });
        Notify(Managers().Append(order.AssigneeUserId), NotificationType.VerificationResult, order, "驗收退回", text);
    }

    public void Cancel(Guid id, int version, string? reason)
    {
        currentUser.RequireUserId();
        var text = InputText.Required(reason, "取消原因", 500);
        var order = Review(id, version, WorkOrderAction.Cancelled, text, target =>
        {
            EnsureNotOutside(target, "取消");
            target.CancelledAt = time.UtcNow;
            target.CancelReason = text;
        });
        audit.Record(AuditActions.CancelWorkOrder, nameof(WorkOrder), order.Id, order.OrderNo, $"取消原因：{text}");
        Notify([order.RequesterUserId, order.AssigneeUserId], NotificationType.WorkOrderStatusChanged, order,
            "已取消", text);
    }

    public void AddComment(Guid id, string? comment)
    {
        currentUser.RequireUserId();
        var text = InputText.Required(comment, "留言內容", 1000);
        businessLock.Run(() =>
        {
            var target = Find(id);
            if (!WorkOrderAccess.CanView(target, currentUser))
                throw new ForbiddenException("只能在自己的工單留言。");
            AddLog(target, WorkOrderAction.Comment, null, null, text);
        });
    }

    public void UpdateChecklist(Guid id, int version, IReadOnlyList<ChecklistEntry> entries)
    {
        currentUser.RequireStaff();
        businessLock.Run(() =>
        {
            var target = FindForProcessing(id, WorkOrderAction.ChecklistUpdated);
            if (target.Checklist.Count == 0) throw new BusinessRuleException("此工單沒有檢查清單。");
            ApplyChecklist(target, entries);
            Transition(target, version, WorkOrderAction.ChecklistUpdated,
                $"已完成 {target.Checklist.Count(c => c.IsChecked)}／{target.Checklist.Count} 項");
        });
    }

    public void IssuePart(Guid id, int version, PartLine line)
    {
        currentUser.RequireStaff();
        var low = businessLock.Run(() =>
        {
            var target = FindForProcessing(id, WorkOrderAction.PartIssued);
            var lines = partService.CheckAvailability([line]);
            // 更新工單版本，重複送出的第二次要求會因版本不符而被拒絕。
            target.Version = version;
            workOrders.Update(target);
            var crossed = partService.Consume(target, lines);
            LogParts(target, lines);
            return crossed;
        });
        partService.NotifyLowStock(low);
    }

    public void SendToVendor(Guid id, int version, SendOutInput input)
    {
        currentUser.RequireStaff();
        var note = InputText.Optional(input.Note, "外送說明", 500);
        businessLock.Run(() =>
        {
            var target = FindForProcessing(id, WorkOrderAction.SentToVendor);
            EnsureNotOutside(target, "再次外送");
            var vendor = vendors.GetById(input.VendorId) is { IsActive: true } v
                ? v
                : throw new BusinessRuleException("請選擇啟用中的廠商。");
            if (input.SentOutDate > time.Today) throw new BusinessRuleException("外送日期不可晚於今天。");
            if (input.SentOutDate < LocalDate(target.CreatedAt))
                throw new BusinessRuleException("外送日期不可早於開單日期。");
            if (input.ExpectedReturnDate < input.SentOutDate)
                throw new BusinessRuleException("預計回廠日不可早於外送日期。");

            target.VendorId = vendor.Id;
            target.SentOutDate = input.SentOutDate;
            target.ExpectedReturnDate = input.ExpectedReturnDate;
            target.ReturnedDate = null;
            Transition(target, version, WorkOrderAction.SentToVendor,
                $"{vendor.Name}，預計 {input.ExpectedReturnDate:yyyy/MM/dd} 回廠" + (note is null ? "" : $"：{note}"));
        });
    }

    public void ReturnFromVendor(Guid id, int version, VendorReturnInput input)
    {
        currentUser.RequireStaff();
        var note = InputText.Optional(input.Note, "送回說明", 500);
        if (input.Cost is < 0 or > 1_000_000_000m) throw new BusinessRuleException("維修費用超出合理範圍。");
        var order = businessLock.Run(() =>
        {
            var target = FindForProcessing(id, WorkOrderAction.ReturnedFromVendor);
            if (target.VendorId is null || target.ReturnedDate is not null)
                throw new BusinessRuleException("此工單目前沒有外送中的維修。");
            if (input.ReturnedDate > time.Today) throw new BusinessRuleException("回廠日期不可晚於今天。");
            if (input.ReturnedDate < target.SentOutDate) throw new BusinessRuleException("回廠日期不可早於外送日期。");

            target.ReturnedDate = input.ReturnedDate;
            target.VendorCost += input.Cost; // 多次外送時累計
            Transition(target, version, WorkOrderAction.ReturnedFromVendor,
                $"費用 NT$ {input.Cost:N0}" + (note is null ? "" : $"：{note}"));
            return target;
        });
        Notify([order.AssigneeUserId], NotificationType.WorkOrderStatusChanged, order, "廠商已送回", "外送設備已回廠。");
    }

    /// <summary>目前狀態不允許該動作時，丟出含原因的例外。</summary>
    internal static void EnsureAllowed(WorkOrder order, WorkOrderAction action)
    {
        if (!WorkOrderStateMachine.Allows(action, order.Status))
            throw new BusinessRuleException(
                $"工單 {order.OrderNo} 目前為「{order.Status.DisplayName()}」，無法執行「{action.DisplayName()}」。");
    }

    /// <summary>技術員（限本人工單）或主管的處理動作。</summary>
    private WorkOrder Process(Guid id, int version, WorkOrderAction action, string? comment,
        Action<WorkOrder>? change = null)
    {
        currentUser.RequireStaff();
        return businessLock.Run(() =>
        {
            var target = FindForProcessing(id, action);
            change?.Invoke(target);
            Transition(target, version, action, comment);
            return target;
        });
    }

    /// <summary>申請人（限本人工單）或主管的驗收、退回與取消；結案或取消後回復資產狀態。</summary>
    private WorkOrder Review(Guid id, int version, WorkOrderAction action, string? comment, Action<WorkOrder> change)
    {
        currentUser.RequireUserId();
        return businessLock.Run(() =>
        {
            var target = Find(id);
            if (!WorkOrderAccess.CanVerify(target, currentUser))
                throw new ForbiddenException("只有申請人或主管可以執行此操作。");
            EnsureAllowed(target, action);
            change(target);
            Transition(target, version, action, comment);
            if (!WorkOrderStateMachine.IsActive(target.Status)) RestoreAsset(target);
            if (target.Status == WorkOrderStatus.Cancelled) maintenance.SkipCycle(target);
            return target;
        });
    }

    private WorkOrder FindForProcessing(Guid id, WorkOrderAction action)
    {
        var order = Find(id);
        if (!WorkOrderAccess.CanProcess(order, currentUser))
            throw new ForbiddenException("只能處理指派給自己的工單。");
        EnsureAllowed(order, action);
        // 主管代為處理時仍需先有負責技術員。
        if (order.AssigneeUserId is null) throw new BusinessRuleException("工單尚未指派技術員。");
        return order;
    }

    /// <summary>套用狀態、以表單版本寫入工單並記錄歷程。狀態不變的動作，歷程不記 From/To。</summary>
    private void Transition(WorkOrder order, int version, WorkOrderAction action, string? comment)
    {
        var from = order.Status;
        var to = WorkOrderStateMachine.ResultOf(action, from);
        order.Status = to;
        order.Version = version;
        workOrders.Update(order);
        var changed = from != to;
        AddLog(order, action, changed ? from : null, changed ? to : null, comment);
    }

    /// <summary>沒有其他未結案報修工單時，維修中的資產回到使用中。保養工單不影響資產狀態。</summary>
    private void RestoreAsset(WorkOrder order)
    {
        if (order.Type != WorkOrderType.Repair) return;
        if (assets.GetById(order.AssetId) is not { Status: AssetStatus.UnderRepair } asset) return;
        if (workOrders.Query(w => w.AssetId == asset.Id && w.Type == WorkOrderType.Repair)
            .Any(w => WorkOrderStateMachine.IsActive(w.Status))) return;
        asset.Status = AssetStatus.InUse;
        assets.Update(asset);
    }

    /// <summary>依清單順序套用勾選與備註；筆數須與工單一致。</summary>
    private static void ApplyChecklist(WorkOrder order, IReadOnlyList<ChecklistEntry> entries)
    {
        if (entries.Count != order.Checklist.Count)
            throw new BusinessRuleException("檢查清單內容不一致，請重新整理頁面後再試。");
        for (var i = 0; i < entries.Count; i++)
        {
            order.Checklist[i].IsChecked = entries[i].IsChecked;
            order.Checklist[i].Note = InputText.Optional(entries[i].Note, "檢查備註", 200);
        }
    }

    /// <summary>完工時每個項目都要勾選，無法完成者需填備註說明原因。</summary>
    private static void EnsureChecklistDone(WorkOrder order)
    {
        if (order.Checklist.FirstOrDefault(c => !c.IsChecked && c.Note is null) is { } item)
            throw new BusinessRuleException($"檢查項目「{item.Text}」尚未勾選；若無法完成，請在備註說明原因。");
    }

    private static void EnsureNotOutside(WorkOrder order, string action)
    {
        if (order.VendorId is not null && order.ReturnedDate is null)
            throw new BusinessRuleException($"設備仍在廠商處，請先登錄「廠商送回」再{action}。");
    }

    private void LogParts(WorkOrder order, IEnumerable<(Part Part, int Quantity)> lines)
    {
        foreach (var (part, quantity) in lines)
            AddLog(order, WorkOrderAction.PartIssued, null, null, $"{part.Name} × {quantity} {part.Unit}");
    }

    private void AddLog(WorkOrder order, WorkOrderAction action, WorkOrderStatus? from, WorkOrderStatus? to,
        string? comment) =>
        logs.Add(new WorkOrderLog
        {
            WorkOrderId = order.Id,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            Comment = comment,
            ActorUserId = currentUser.UserId,
            OccurredAt = time.UtcNow
        });

    private void Notify(IEnumerable<Guid?> userIds, NotificationType type, WorkOrder order, string headline,
        string message) =>
        notifications.Send(userIds, type, $"工單 {order.OrderNo} {headline}",
            $"{assets.GetById(order.AssetId)?.Name}「{order.Title}」{message}", WorkOrderLinks.Details(order.Id));

    private IEnumerable<Guid?> Managers() => notifications.ManagerIds().Select(id => (Guid?)id);

    private bool AutoSetUnderRepair() => !string.Equals(
        settings.GetValue(SettingKeys.AutoSetAssetUnderRepair), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>回應與完工時限（小時）；設定缺少時用需求的預設值。</summary>
    private (int Response, int Resolve) SlaHours(Priority priority)
    {
        var (response, resolve) = priority switch
        {
            Priority.Urgent => (2, 8),
            Priority.High => (4, 24),
            Priority.Normal => (8, 72),
            _ => (24, 168)
        };
        return (settings.GetInt(SettingKeys.SlaResponseHours(priority), response),
            settings.GetInt(SettingKeys.SlaResolveHours(priority), resolve));
    }

    private DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(time.ToLocal(utc));

    private static string TitleFrom(string description)
    {
        var line = description.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        return line.Length <= MaxTitleLength ? line : line[..(MaxTitleLength - 1)] + "…";
    }

    private WorkOrder Find(Guid id) => workOrders.GetById(id) ?? throw new NotFoundException("找不到工單。");
}
