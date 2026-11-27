using AssetCare.Application;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Rules;
using Microsoft.AspNetCore.Identity;

namespace AssetCare.Infrastructure.Json.Seeding;

/// <summary>
/// 依 catalog.json 批次產生範例資料。亂數種子固定；所有日期相對於執行當下推算。
/// （密碼雜湊的鹽值由 PasswordHasher 隨機產生，因此每次不同。）
/// </summary>
public class SeedGenerator(SeedCatalog catalog, ITimeProvider time, IPasswordHasher<User> hasher)
{
    private const int RandomSeed = 20260101;

    private readonly Random _random = new(RandomSeed);
    private readonly SeedDataSet _data = new();
    private readonly DateTime _now = Truncate(time.UtcNow);
    private readonly DateOnly _today = time.Today;

    private readonly Dictionary<string, User> _users = [];
    private readonly Dictionary<string, Department> _departments = [];
    private readonly Dictionary<string, Location> _locations = [];
    private readonly Dictionary<string, AssetCategory> _categories = [];
    private readonly Dictionary<string, Vendor> _vendors = [];
    private readonly Dictionary<string, Asset> _assets = [];
    private readonly Dictionary<string, Part> _parts = [];
    private readonly Dictionary<string, MaintenancePlan> _plans = [];
    private readonly Dictionary<string, WorkOrder> _workOrders = [];
    private readonly List<PartTransaction> _transactions = [];

    public SeedDataSet Generate()
    {
        AddSettings();
        AddOrganization();
        AddAssets();
        AddTransfers();
        AddPartsAndStockIns();
        AddPlans();
        AddWorkOrders();
        FinishPartTransactions();
        ApplyAssetStatus();
        AddAssetAttachments();
        AddNotifications();
        AddAuditLogs();
        return _data;
    }

    private void AddSettings()
    {
        foreach (var s in catalog.Settings)
            Add(new AppSetting { Key = s.Key, Value = s.Value, Description = s.Description }, DaysAgo(400));
    }

    private void AddOrganization()
    {
        foreach (var d in catalog.Departments)
            _departments[d.Key] = Add(new Department { Code = d.Key, Name = d.Name }, DaysAgo(400));

        foreach (var u in catalog.Users)
        {
            var user = new User
            {
                UserName = u.Key,
                DisplayName = u.Name,
                Email = $"{u.Key}@{catalog.EmailDomain}",
                Phone = u.Phone,
                Role = u.Role,
                DepartmentId = _departments[u.Dept].Id,
                IsActive = !u.Disabled,
                SecurityStamp = NextGuid().ToString("N")
            };
            user.PasswordHash = hasher.HashPassword(user, catalog.DemoPassword);
            _users[u.Key] = Add(user, DaysAgo(400));
        }

        foreach (var d in catalog.Departments.Where(d => d.Manager is not null))
            _departments[d.Key].ManagerUserId = _users[d.Manager!].Id;

        var order = 0;
        foreach (var l in catalog.Locations)
            _locations[l.Key] = Add(new Location
            {
                Code = l.Key,
                Name = l.Name,
                ParentId = l.Parent is null ? null : _locations[l.Parent].Id,
                SortOrder = ++order * 10
            }, DaysAgo(400));

        foreach (var c in catalog.Categories)
            _categories[c.Key] = Add(new AssetCategory
            {
                Code = c.Key,
                Name = c.Name,
                Description = c.Description,
                DefaultUsefulLifeYears = c.Life
            }, DaysAgo(400));

        foreach (var v in catalog.Vendors)
            _vendors[v.Key] = Add(new Vendor
            {
                Name = v.Name,
                TaxId = v.TaxId,
                ContactName = v.Contact,
                Phone = v.Phone,
                Email = $"service@{v.Key}.{catalog.EmailDomain}",
                Address = v.Address,
                ServiceScope = v.Scope
            }, DaysAgo(380));
    }

    private void AddAssets()
    {
        var sequence = new Dictionary<string, int>();
        foreach (var a in catalog.Assets)
        {
            var number = sequence[a.Category] = sequence.GetValueOrDefault(a.Category) + 1;
            var purchase = _today.AddMonths(-a.AgeMonths).AddDays(-_random.Next(0, 25));
            DateOnly? warranty = a.Warranty switch
            {
                "active" => _today.AddDays(_random.Next(120, 900)),
                "expiring" or "expired" => _today.AddDays(a.WarrantyOffsetDays!.Value),
                _ => null
            };
            var scrapped = a.State == "scrapped";
            _assets[a.Key] = Add(new Asset
            {
                AssetNo = $"AST-{a.Category}-{number:0000}",
                Name = a.Name,
                CategoryId = _categories[a.Category].Id,
                Status = a.State switch { "scrapped" => AssetStatus.Scrapped, "idle" => AssetStatus.Idle, _ => AssetStatus.InUse },
                Brand = a.Brand,
                Model = a.Model,
                SerialNo = $"{a.Category}{_random.Next(10_000_000, 99_999_999)}",
                PurchaseDate = purchase,
                PurchaseCost = a.Cost,
                SalvageValue = Math.Round(a.Cost * 0.1m, 0),
                UsefulLifeYears = _categories[a.Category].DefaultUsefulLifeYears,
                WarrantyEndDate = warranty,
                LocationId = _locations[a.Location].Id,
                DepartmentId = _departments[a.Dept].Id,
                CustodianUserId = a.Custodian is null ? null : _users[a.Custodian].Id,
                VendorId = _vendors[a.Vendor].Id,
                UsageUnit = a.UsageUnit,
                CurrentUsage = a.CurrentUsage,
                ScrappedAt = scrapped ? DaysAgo(a.ScrappedDaysAgo!.Value) : null,
                ScrapReason = a.ScrapReason
            }, Min(time.ToUtc(purchase.ToDateTime(new TimeOnly(10, 0))), DaysAgo(30)));
        }
    }

    private void AddTransfers()
    {
        foreach (var t in catalog.Transfers.OrderByDescending(t => t.DaysAgo))
        {
            var asset = _assets[t.Asset];
            var at = DaysAgo(t.DaysAgo);
            Add(new AssetTransfer
            {
                AssetId = asset.Id,
                FromLocationId = _locations[t.FromLocation].Id,
                ToLocationId = asset.LocationId,
                FromDepartmentId = _departments[t.FromDept].Id,
                ToDepartmentId = asset.DepartmentId,
                FromCustodianUserId = _users[t.FromCustodian].Id,
                ToCustodianUserId = asset.CustodianUserId,
                Reason = t.Reason,
                TransferredByUserId = _users[t.By].Id,
                TransferredAt = at
            }, at);
        }
    }

    private void AddPartsAndStockIns()
    {
        var number = 0;
        foreach (var p in catalog.Parts)
            _parts[p.Key] = Add(new Part
            {
                PartNo = $"PRT-{++number:0000}",
                Name = p.Name,
                Spec = p.Spec,
                Unit = p.Unit,
                UnitCost = p.Cost,
                SafetyStock = p.Safety,
                StockQuantity = p.Stock,
                VendorId = _vendors[p.Vendor].Id,
                StorageLocation = p.Shelf
            }, DaysAgo(300));

        foreach (var s in catalog.StockIns)
        {
            var at = DaysAgo(s.DaysAgo);
            _transactions.Add(Add(new PartTransaction
            {
                PartId = _parts[s.Part].Id,
                Type = PartTransactionType.StockIn,
                Quantity = s.Qty,
                UnitCost = _parts[s.Part].UnitCost,
                OperatorUserId = _users[s.By].Id,
                Note = s.Note,
                OccurredAt = at
            }, at));
        }
    }

    private void AddPlans()
    {
        foreach (var p in catalog.Plans)
            _plans[p.Key] = Add(new MaintenancePlan
            {
                Name = p.Name,
                AssetId = _assets[p.Asset].Id,
                Cycle = p.Cycle,
                Priority = p.Priority,
                LeadDays = p.LeadDays,
                NextDueDate = p.DueOffsetDays is { } due ? _today.AddDays(due) : null,
                LastCompletedDate = p.LastOffsetDays is { } last ? _today.AddDays(last) : null,
                UsageInterval = p.UsageInterval,
                NextDueUsage = p.NextDueUsage,
                AssigneeUserId = _users[p.Assignee].Id,
                ChecklistItems = [.. p.Checklist]
            }, DaysAgo(250));
    }

    private void AddWorkOrders()
    {
        var sequence = new Dictionary<string, int>();
        // 依建立時間先後配號。
        foreach (var w in catalog.WorkOrders.OrderByDescending(w => w.CreatedHoursAgo))
        {
            var created = HoursAgo(w.CreatedHoursAgo);
            var preventive = w.Plan is not null;
            var plan = preventive ? _plans[w.Plan!] : null;
            var prefix = $"{(preventive ? "PM" : "WO")}-{time.ToLocal(created):yyyyMMdd}";
            var number = sequence[prefix] = sequence.GetValueOrDefault(prefix) + 1;
            var dueDate = plan?.NextDueDate;

            var order = Add(new WorkOrder
            {
                OrderNo = $"{prefix}-{number:0000}",
                Type = preventive ? WorkOrderType.Preventive : WorkOrderType.Repair,
                Status = WorkOrderStatus.Open,
                Priority = w.Priority,
                AssetId = _assets[w.Asset].Id,
                Title = w.Title,
                Description = w.Description,
                RequesterUserId = w.Requester is null ? null : _users[w.Requester].Id,
                ResponseDueAt = preventive ? null : created.AddHours(Setting(SettingKeys.SlaResponseHours(w.Priority))),
                ResolveDueAt = preventive
                    ? time.ToUtc(dueDate!.Value.ToDateTime(new TimeOnly(23, 59, 59)))
                    : created.AddHours(Setting(SettingKeys.SlaResolveHours(w.Priority))),
                MaintenancePlanId = plan?.Id,
                MaintenanceCycleKey = dueDate?.ToString("yyyy-MM-dd"),
                Checklist = plan?.ChecklistItems.Select(text => new ChecklistItem { Text = text }).ToList() ?? []
            }, created);
            _workOrders[w.Key] = order;

            AddLog(order, WorkOrderAction.Created, null, WorkOrderStatus.Open, null, order.RequesterUserId, created);
            var last = created;
            foreach (var step in w.Steps)
            {
                // 加上少量亂數分鐘，讓時間看起來自然，同時保持步驟先後順序。
                var at = created.AddHours(step.At).AddSeconds(_random.Next(0, 240));
                if (at <= last) at = last.AddSeconds(30);
                last = at = Min(at, _now);
                ApplyStep(order, w, step, at);
            }

            for (var i = 1; i <= w.Photos; i++)
                AddImage(AttachmentOwnerType.WorkOrder, order.Id, $"現場照片{i}.png", null,
                    order.RequesterUserId!.Value, created.AddMinutes(i));

            order.IsResponseOverdue = order.Status != WorkOrderStatus.Cancelled &&
                SlaRules.IsOverdue(order.ResponseDueAt, order.RespondedAt, _now);
            order.IsResolveOverdue = order.Status != WorkOrderStatus.Cancelled &&
                SlaRules.IsOverdue(order.ResolveDueAt, order.CompletedAt, _now);
            if (order.IsResponseOverdue) order.ResponseOverdueNotifiedAt = Min(order.ResponseDueAt!.Value.AddMinutes(5), _now);
            if (order.IsResolveOverdue) order.ResolveOverdueNotifiedAt = Min(order.ResolveDueAt.AddMinutes(5), _now);
            order.UpdatedAt = last;
        }
    }

    private void ApplyStep(WorkOrder order, CatalogWorkOrder source, CatalogStep step, DateTime at)
    {
        Guid? actor = step.By is null ? null : _users[step.By].Id;

        void Transition(WorkOrderAction action, WorkOrderStatus to)
        {
            AddLog(order, action, order.Status, to, step.Note, actor, at);
            order.Status = to;
        }

        switch (step.Do)
        {
            case "assign":
                order.AssigneeUserId = _users[step.To!].Id;
                order.RespondedAt = at;
                Transition(WorkOrderAction.Assigned, WorkOrderStatus.Assigned);
                break;
            case "accept":
                order.AssigneeUserId = actor;
                order.RespondedAt = at;
                Transition(WorkOrderAction.Accepted, WorkOrderStatus.Assigned);
                break;
            case "start":
                order.StartedAt = at;
                Transition(WorkOrderAction.Started, WorkOrderStatus.InProgress);
                break;
            case "wait":
                Transition(WorkOrderAction.WaitingParts, WorkOrderStatus.WaitingParts);
                break;
            case "resume":
                Transition(WorkOrderAction.Resumed, WorkOrderStatus.InProgress);
                break;
            case "complete":
                order.CompletedAt = at;
                order.FaultCause = source.Cause;
                order.Resolution = source.Resolution;
                order.LaborHours = source.LaborHours;
                foreach (var item in order.Checklist) item.IsChecked = true;
                Transition(WorkOrderAction.Completed, WorkOrderStatus.Completed);
                if (source.Plan is not null) CompletePlan(_plans[source.Plan], at);
                break;
            case "reject":
                // 退回前保留當次完工填寫內容供技術員參考。
                order.CompletedAt = null;
                order.ReworkCount++;
                Transition(WorkOrderAction.Rejected, WorkOrderStatus.InProgress);
                break;
            case "verify":
                order.ClosedAt = at;
                Transition(WorkOrderAction.Verified, WorkOrderStatus.Closed);
                break;
            case "cancel":
                order.CancelledAt = at;
                order.CancelReason = step.Note;
                Transition(WorkOrderAction.Cancelled, WorkOrderStatus.Cancelled);
                break;
            case "comment":
                AddLog(order, WorkOrderAction.Comment, null, null, step.Note, actor, at);
                break;
            case "sendout":
                order.VendorId = _vendors[step.Vendor!].Id;
                order.SentOutDate = LocalDate(at);
                order.ExpectedReturnDate = LocalDate(at).AddDays(step.ReturnInDays);
                order.VendorCost = source.VendorCost;
                AddLog(order, WorkOrderAction.SentToVendor, null, null, step.Note, actor, at);
                break;
            case "return":
                order.ReturnedDate = LocalDate(at);
                AddLog(order, WorkOrderAction.ReturnedFromVendor, null, null, step.Note, actor, at);
                break;
            case "issue":
                IssuePart(order, _parts[step.Part!], step.Qty, actor!.Value, at);
                break;
            default:
                throw new InvalidOperationException($"catalog.json 工單 {source.Key} 有未知步驟：{step.Do}");
        }
    }

    private void CompletePlan(MaintenancePlan plan, DateTime completedAt)
    {
        var date = LocalDate(completedAt);
        plan.LastCompletedDate = date;
        plan.NextDueDate = MaintenanceRules.NextDueDate(plan.Cycle, date);
        plan.UpdatedAt = completedAt;
    }

    private void IssuePart(WorkOrder order, Part part, int quantity, Guid actor, DateTime at)
    {
        Add(new WorkOrderPart
        {
            WorkOrderId = order.Id,
            PartId = part.Id,
            Quantity = quantity,
            UnitCost = part.UnitCost,
            IssuedByUserId = actor,
            IssuedAt = at
        }, at);
        _transactions.Add(Add(new PartTransaction
        {
            PartId = part.Id,
            Type = PartTransactionType.Issue,
            Quantity = -quantity,
            UnitCost = part.UnitCost,
            WorkOrderId = order.Id,
            OperatorUserId = actor,
            Note = $"工單 {order.OrderNo} 領用",
            OccurredAt = at
        }, at));
        AddLog(order, WorkOrderAction.PartIssued, null, null, $"{part.Name} × {quantity} {part.Unit}", actor, at);
    }

    /// <summary>由目前庫存反推每筆異動後的結餘。</summary>
    private void FinishPartTransactions()
    {
        foreach (var group in _transactions.GroupBy(t => t.PartId))
        {
            var balance = _data.Set<Part>().Single(p => p.Id == group.Key).StockQuantity;
            foreach (var transaction in group.OrderByDescending(t => t.OccurredAt))
            {
                transaction.BalanceAfter = balance;
                balance -= transaction.Quantity;
            }
        }
        _data.Set<PartTransaction>().Sort((a, b) => a.OccurredAt.CompareTo(b.OccurredAt));
    }

    /// <summary>有未結案報修工單的資產設為維修中（保養工單不影響資產狀態）。</summary>
    private void ApplyAssetStatus()
    {
        foreach (var asset in _assets.Values.Where(a => a.Status == AssetStatus.InUse))
            if (_workOrders.Values.Any(w => w.AssetId == asset.Id && w.Type == WorkOrderType.Repair &&
                                            WorkOrderStateMachine.IsActive(w.Status)))
                asset.Status = AssetStatus.UnderRepair;
    }

    private void AddAssetAttachments()
    {
        foreach (var a in catalog.AssetAttachments)
            AddImage(AttachmentOwnerType.Asset, _assets[a.Asset].Id, a.FileName, a.Caption, _users[a.By].Id, DaysAgo(a.DaysAgo));
    }

    private void AddNotifications()
    {
        foreach (var n in catalog.Notifications.OrderByDescending(n => n.HoursAgo))
        {
            var user = _users[n.User];
            var at = HoursAgo(n.HoursAgo);
            var order = n.WorkOrder is null ? null : _workOrders[n.WorkOrder];
            var plan = n.Plan is null ? null : _plans[n.Plan];
            var part = n.Part is null ? null : _parts[n.Part];
            var asset = n.Asset is not null ? _assets[n.Asset]
                : order is not null ? _assets.Values.Single(a => a.Id == order.AssetId)
                : plan is not null ? _assets.Values.Single(a => a.Id == plan.AssetId)
                : null;

            string Fill(string template) => template
                .Replace("{orderNo}", order?.OrderNo)
                .Replace("{title}", order?.Title)
                .Replace("{assetNo}", asset?.AssetNo)
                .Replace("{assetName}", asset?.Name)
                .Replace("{planName}", plan?.Name)
                .Replace("{partName}", part?.Name);

            var notification = Add(new Notification
            {
                UserId = user.Id,
                Type = n.Type,
                Title = Fill(n.Title),
                Message = Fill(n.Message),
                LinkUrl = order is not null ? $"/WorkOrders/Details/{order.Id}"
                    : plan is not null ? $"/MaintenancePlans/Details/{plan.Id}"
                    : part is not null ? $"/Parts/Details/{part.Id}"
                    : $"/Assets/Details/{asset!.Id}",
                IsRead = n.Read,
                ReadAt = n.Read ? Min(at.AddMinutes(_random.Next(10, 180)), _now) : null
            }, at);

            if (!n.Email) continue;
            Add(new EmailLog
            {
                ToUserId = user.Id,
                ToEmail = user.Email,
                Subject = notification.Title,
                Body = notification.Message,
                FileName = $"{at:yyyyMMddHHmmss}_{notification.Id.ToString("N")[..8]}.eml",
                Status = n.EmailFailed ? EmailStatus.Failed : EmailStatus.Sent,
                ErrorMessage = n.EmailFailed ? "無法寫入 outbox 資料夾" : null,
                SentAt = at,
                NotificationId = notification.Id
            }, at);
        }
    }

    private void AddAuditLogs()
    {
        foreach (var a in catalog.AuditLogs.OrderByDescending(a => a.HoursAgo))
        {
            var user = _users[a.User];
            var at = HoursAgo(a.HoursAgo);
            (Guid? Id, string? Name) target = a switch
            {
                { WorkOrder: { } w } => (_workOrders[w].Id, _workOrders[w].OrderNo),
                { Asset: { } s } => (_assets[s].Id, _assets[s].AssetNo),
                { Part: { } p } => (_parts[p].Id, _parts[p].Name),
                { TargetUser: { } u } => (_users[u].Id, _users[u].UserName),
                _ when a.Entity == nameof(User) => (user.Id, user.UserName),
                _ => (null, null)
            };
            Add(new AuditLog
            {
                UserId = user.Id,
                UserName = user.UserName,
                OccurredAt = at,
                IpAddress = $"192.168.10.{_random.Next(20, 220)}",
                Action = a.Action,
                EntityType = a.Entity,
                EntityId = target.Id,
                EntityName = target.Name,
                Detail = a.Detail
            }, at);
            if (a.Action == AuditActions.Login && !(user.LastLoginAt >= at)) user.LastLoginAt = at;
        }
    }

    private void AddImage(AttachmentOwnerType ownerType, Guid ownerId, string fileName, string? caption, Guid uploader, DateTime at)
    {
        var stored = NextGuid().ToString("N") + ".png";
        var content = PlaceholderImage.Create(_random);
        _data.Uploads[stored] = content;
        Add(new Attachment
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            FileName = fileName,
            StoredFileName = stored,
            ContentType = "image/png",
            SizeBytes = content.Length,
            Caption = caption,
            UploadedByUserId = uploader
        }, Min(at, _now));
    }

    private void AddLog(WorkOrder order, WorkOrderAction action, WorkOrderStatus? from, WorkOrderStatus? to,
        string? comment, Guid? actor, DateTime at) =>
        Add(new WorkOrderLog
        {
            WorkOrderId = order.Id,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            Comment = comment,
            ActorUserId = actor,
            OccurredAt = at
        }, at);

    private T Add<T>(T entity, DateTime createdAt) where T : BaseEntity
    {
        entity.Id = NextGuid();
        entity.Version = 1;
        entity.CreatedAt = entity.UpdatedAt = createdAt;
        _data.Set<T>().Add(entity);
        return entity;
    }

    private Guid NextGuid()
    {
        var bytes = new byte[16];
        _random.NextBytes(bytes);
        return new Guid(bytes);
    }

    private int Setting(string key) => int.Parse(catalog.Settings.Single(s => s.Key == key).Value);

    private DateTime HoursAgo(double hours) => Truncate(_now.AddHours(-hours));
    private DateTime DaysAgo(int days) => HoursAgo(days * 24);
    private DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(time.ToLocal(utc));
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    private static DateTime Truncate(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
}
