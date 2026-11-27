using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class PartService(
    IRepository<Part> parts,
    IRepository<PartTransaction> transactions,
    IRepository<WorkOrderPart> orderParts,
    IRepository<WorkOrder> workOrders,
    IRepository<Vendor> vendors,
    IRepository<User> users,
    INotificationDispatcher notifications,
    IBusinessLock businessLock,
    ICurrentUser currentUser,
    IAuditService audit,
    ITimeProvider time) : IPartService
{
    private const int PageSize = 10;
    private const int MaxQuantity = 1_000_000;

    public PagedResult<PartListItem> Search(PartQuery query)
    {
        currentUser.RequireStaff();
        IEnumerable<Part> source = parts.GetAll();
        if (!query.IncludeInactive) source = source.Where(p => p.IsActive);
        if (query.LowStockOnly) source = source.Where(PartRules.IsLowStock);
        if (query.Keyword?.Trim() is { Length: > 0 } keyword)
            source = source.Where(p => new[] { p.PartNo, p.Name, p.Spec, p.StorageLocation }
                .Any(v => v?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true));

        // 低於安全量者排在前面
        var items = source.OrderByDescending(PartRules.IsLowStock).ThenBy(p => p.PartNo, StringComparer.Ordinal)
            .Select(p => new PartListItem(p.Id, p.PartNo, p.Name, p.Spec, p.Unit, p.StockQuantity, p.SafetyStock,
                p.UnitCost, p.StorageLocation, p.IsActive, PartRules.IsLowStock(p)))
            .ToList();
        return PagedResult<PartListItem>.From(items, query.Page, PageSize);
    }

    public PartDetail GetDetail(Guid id)
    {
        currentUser.RequireStaff();
        var part = Find(id);
        var userNames = users.GetAll().ToDictionary(u => u.Id, u => u.DisplayName);
        var orders = workOrders.GetAll().ToDictionary(w => w.Id);
        var vendor = part.VendorId is { } v ? vendors.GetById(v) : null;

        var history = transactions.Query(t => t.PartId == id)
            .OrderByDescending(t => t.OccurredAt)
            .Select(t =>
            {
                var order = t.WorkOrderId is { } w ? orders.GetValueOrDefault(w) : null;
                return new PartTransactionItem(t.OccurredAt, t.Type, t.Quantity, t.BalanceAfter, t.UnitCost,
                    order?.Id, order?.OrderNo, userNames.GetValueOrDefault(t.OperatorUserId, "—"), t.Note);
            }).ToList();

        List<IssueTarget> targets = !part.IsActive ? [] : orders.Values
            .Where(w => WorkOrderStateMachine.Allows(WorkOrderAction.PartIssued, w.Status) &&
                        WorkOrderAccess.CanProcess(w, currentUser))
            .OrderBy(w => w.OrderNo)
            .Select(w => new IssueTarget(w.Id, w.Version, $"{w.OrderNo} {w.Title}"))
            .ToList();

        return new PartDetail(part.Id, part.Version, part.PartNo, part.Name, part.Spec, part.Unit, part.StockQuantity,
            part.SafetyStock, part.UnitCost, vendor?.Name, vendor?.Phone, part.StorageLocation, part.IsActive,
            PartRules.IsLowStock(part), history, targets);
    }

    public IReadOnlyList<Option> GetVendorOptions()
    {
        currentUser.RequireManager();
        return vendors.GetAll().OrderByDescending(v => v.IsActive).ThenBy(v => v.Name)
            .Select(v => new Option(v.Id, v.IsActive ? v.Name : $"{v.Name}（停用）")).ToList();
    }

    public PartEditData GetForEdit(Guid id)
    {
        currentUser.RequireManager();
        var p = Find(id);
        return new PartEditData(p.Id, p.Version, p.PartNo, new PartInput(p.Name, p.Spec, p.Unit, p.SafetyStock,
            p.UnitCost, p.VendorId, p.StorageLocation, p.IsActive, p.StockQuantity));
    }

    public Guid Create(PartInput input)
    {
        currentUser.RequireManager();
        EnsureQuantity(input.InitialStock, "期初庫存", allowZero: true);
        var created = businessLock.Run(() =>
        {
            var part = new Part();
            Apply(part, input);
            part.PartNo = NextPartNo();
            part.StockQuantity = input.InitialStock;
            parts.Add(part);
            if (input.InitialStock > 0)
                Record(part, PartTransactionType.StockIn, input.InitialStock, null, "期初庫存");
            audit.Record(AuditActions.Create, nameof(Part), part.Id, part.Name, $"新增零件 {part.PartNo}");
            return part;
        });
        if (PartRules.IsLowStock(created)) NotifyLowStock([created]);
        return created.Id;
    }

    public void Update(Guid id, int version, PartInput input)
    {
        currentUser.RequireManager();
        ChangeAndNotify(id, part =>
        {
            Apply(part, input);
            part.Version = version;
            parts.Update(part);
            audit.Record(AuditActions.Update, nameof(Part), part.Id, part.Name, "修改零件資料");
        });
    }

    public void StockIn(Guid id, int version, int quantity, decimal unitCost, string? note)
    {
        currentUser.RequireManager();
        EnsureQuantity(quantity, "入庫數量", allowZero: false);
        EnsureCost(unitCost);
        var text = InputText.Optional(note, "備註", 200);
        ChangeAndNotify(id, part =>
        {
            if (!part.IsActive) throw new BusinessRuleException("此零件已停用，無法入庫。");
            if (part.StockQuantity + (long)quantity > MaxQuantity) throw new BusinessRuleException("庫存數量超出上限。");
            part.StockQuantity += quantity;
            part.UnitCost = unitCost; // 以最近一次進價作為之後領用的單價
            part.Version = version;
            parts.Update(part);
            Record(part, PartTransactionType.StockIn, quantity, null, text);
            audit.Record(AuditActions.StockIn, nameof(Part), part.Id, part.Name, $"入庫 {quantity} {part.Unit}");
        });
    }

    public void Adjust(Guid id, int version, int actualQuantity, string? note)
    {
        currentUser.RequireManager();
        EnsureQuantity(actualQuantity, "實際數量", allowZero: true);
        var text = InputText.Required(note, "調整原因", 200);
        ChangeAndNotify(id, part =>
        {
            var delta = actualQuantity - part.StockQuantity;
            if (delta == 0) throw new BusinessRuleException("實際數量與帳面庫存相同，不需調整。");
            part.StockQuantity = actualQuantity;
            part.Version = version;
            parts.Update(part);
            Record(part, PartTransactionType.Adjust, delta, null, text);
            audit.Record(AuditActions.AdjustStock, nameof(Part), part.Id, part.Name,
                $"盤點調整 {delta:+#;-#} {part.Unit}，原因：{text}");
        });
    }

    // ---- 以下供工單流程領料使用，呼叫端需在 IBusinessLock 內 ----

    /// <summary>檢查全部領料項目（同一零件合併計算）；任一不足即丟出例外，不寫入任何資料。</summary>
    internal List<(Part Part, int Quantity)> CheckAvailability(IEnumerable<PartLine> lines)
    {
        var result = new List<(Part, int)>();
        var errors = new List<string>();
        foreach (var group in lines.GroupBy(l => l.PartId))
        {
            if (group.Any(l => l.Quantity is <= 0 or > MaxQuantity))
                throw new BusinessRuleException("領用數量必須大於 0。");
            var quantity = group.Sum(l => (long)l.Quantity);
            var part = parts.GetById(group.Key) is { IsActive: true } p
                ? p
                : throw new BusinessRuleException("請選擇啟用中的零件。");
            if (quantity > part.StockQuantity)
                errors.Add($"{part.Name} 庫存不足（現有 {part.StockQuantity} {part.Unit}，需要 {quantity}）");
            else
                result.Add((part, (int)quantity));
        }
        return errors.Count == 0 ? result : throw new BusinessRuleException(string.Join("；", errors) + "。");
    }

    /// <summary>扣庫存並建立工單用料與異動紀錄。回傳本次降到安全量以下的零件。</summary>
    internal List<Part> Consume(WorkOrder order, IReadOnlyList<(Part Part, int Quantity)> lines)
    {
        var crossed = new List<Part>();
        foreach (var (part, quantity) in lines)
        {
            var wasLow = PartRules.IsLowStock(part);
            part.StockQuantity -= quantity;
            parts.Update(part);
            orderParts.Add(new WorkOrderPart
            {
                WorkOrderId = order.Id,
                PartId = part.Id,
                Quantity = quantity,
                UnitCost = part.UnitCost,
                IssuedByUserId = currentUser.RequireUserId(),
                IssuedAt = time.UtcNow
            });
            Record(part, PartTransactionType.Issue, -quantity, order.Id, $"工單 {order.OrderNo} 領用");
            if (!wasLow && PartRules.IsLowStock(part)) crossed.Add(part);
        }
        return crossed;
    }

    internal void NotifyLowStock(IEnumerable<Part> lowParts)
    {
        foreach (var part in lowParts)
            notifications.Send(notifications.ManagerIds().Select(id => (Guid?)id), NotificationType.LowStock,
                $"庫存低於安全量：{part.Name}",
                $"目前庫存 {part.StockQuantity} {part.Unit}，安全量 {part.SafetyStock} {part.Unit}，請安排採購。",
                $"/Parts/Details/{part.Id}");
    }

    /// <summary>在鎖內執行異動；若因此降到安全量以下，於鎖外發送通知。</summary>
    private void ChangeAndNotify(Guid id, Action<Part> change)
    {
        var low = businessLock.Run(() =>
        {
            var part = Find(id);
            var wasLow = PartRules.IsLowStock(part);
            change(part);
            return !wasLow && PartRules.IsLowStock(part) ? part : null;
        });
        if (low is not null) NotifyLowStock([low]);
    }

    private void Record(Part part, PartTransactionType type, int quantity, Guid? workOrderId, string? note) =>
        transactions.Add(new PartTransaction
        {
            PartId = part.Id,
            Type = type,
            Quantity = quantity,
            BalanceAfter = part.StockQuantity,
            UnitCost = part.UnitCost,
            WorkOrderId = workOrderId,
            OperatorUserId = currentUser.RequireUserId(),
            Note = note,
            OccurredAt = time.UtcNow
        });

    private string NextPartNo()
    {
        const string prefix = "PRT-";
        var max = parts.GetAll()
            .Select(p => p.PartNo.StartsWith(prefix, StringComparison.Ordinal) &&
                         int.TryParse(p.PartNo.AsSpan(prefix.Length), out var n) ? n : 0)
            .DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:0000}";
    }

    private Part Find(Guid id) => parts.GetById(id) ?? throw new NotFoundException("找不到零件。");

    private void Apply(Part part, PartInput input)
    {
        EnsureQuantity(input.SafetyStock, "安全庫存", allowZero: true);
        EnsureCost(input.UnitCost);
        if (input.VendorId is { } v && vendors.GetById(v) is null) throw new BusinessRuleException("找不到指定的廠商。");

        var name = InputText.Required(input.Name, "零件名稱", 100);
        var spec = InputText.Optional(input.Spec, "規格", 100);
        if (parts.GetAll().Any(p => p.Id != part.Id && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(p.Spec, spec, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException($"相同名稱與規格的零件「{name}」已存在。");

        part.Name = name;
        part.Spec = spec;
        part.Unit = InputText.Required(input.Unit, "單位", 10);
        part.SafetyStock = input.SafetyStock;
        part.UnitCost = input.UnitCost;
        part.VendorId = input.VendorId;
        part.StorageLocation = InputText.Optional(input.StorageLocation, "存放位置", 50);
        part.IsActive = input.IsActive;
    }

    private static void EnsureQuantity(int value, string label, bool allowZero)
    {
        var min = allowZero ? 0 : 1;
        if (value < min || value > MaxQuantity)
            throw new BusinessRuleException($"{label}須介於 {min} 到 {MaxQuantity:N0}。");
    }

    private static void EnsureCost(decimal value)
    {
        if (value is < 0 or > 1_000_000_000m) throw new BusinessRuleException("單價超出合理範圍。");
    }
}
