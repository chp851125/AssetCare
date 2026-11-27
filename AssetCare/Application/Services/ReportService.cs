using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class ReportService(
    IRepository<WorkOrder> workOrders,
    IRepository<WorkOrderPart> orderParts,
    IRepository<Asset> assets,
    IRepository<AssetCategory> categories,
    IRepository<Location> locations,
    IRepository<Department> departments,
    IRepository<User> users,
    IRepository<Vendor> vendors,
    IRepository<MaintenancePlan> plans,
    IRepository<Part> parts,
    IRepository<PartTransaction> transactions,
    IAssetExcelService assetExcel,
    ISpreadsheetService spreadsheet,
    ICurrentUser currentUser,
    IAuditService audit,
    ITimeProvider time) : IReportService
{
    private const int MaxRangeDays = 1100;
    private const int DefaultRangeDays = 90;

    public ReportRange GetDefaultRange()
    {
        currentUser.RequireManager();
        return new ReportRange(time.Today.AddDays(-(DefaultRangeDays - 1)), time.Today);
    }

    public byte[] Export(ReportType type, ReportRange range)
    {
        currentUser.RequireManager();
        // 資產清冊沿用資產匯出（含其稽核紀錄）。
        if (type == ReportType.Assets) return assetExcel.Export(new AssetQuery());
        if (!Enum.IsDefined(type)) throw new BusinessRuleException("請選擇報表類型。");
        if (range.To < range.From) throw new BusinessRuleException("結束日期不可早於開始日期。");
        if (range.To.DayNumber - range.From.DayNumber > MaxRangeDays)
            throw new BusinessRuleException("報表期間最長 3 年。");

        var sheets = type switch
        {
            ReportType.WorkOrders => [WorkOrderSheet(range)],
            ReportType.Maintenance => [MaintenanceSheet(range)],
            ReportType.PartTransactions => [PartSheet(range)],
            _ => CostSheets(range)
        };
        audit.Record(AuditActions.Export, "Report", entityName: type.DisplayName(),
            detail: $"期間 {range.From:yyyy/MM/dd}–{range.To:yyyy/MM/dd}，{sheets[0].Rows.Count} 筆");
        return spreadsheet.Write(sheets);
    }

    private SheetData WorkOrderSheet(ReportRange range)
    {
        var assetMap = assets.GetAll().ToDictionary(a => a.Id);
        var categoryNames = categories.GetAll().ToDictionary(c => c.Id, c => c.Name);
        var hierarchy = new LocationHierarchy(locations.GetAll());
        var userMap = users.GetAll().ToDictionary(u => u.Id);
        var departmentNames = departments.GetAll().ToDictionary(d => d.Id, d => d.Name);
        var vendorNames = vendors.GetAll().ToDictionary(v => v.Id, v => v.Name);
        var partCost = orderParts.GetAll().GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Quantity * p.UnitCost));
        var now = time.UtcNow;

        var rows = workOrders.GetAll().Where(w => InRange(w.CreatedAt, range)).OrderBy(w => w.CreatedAt).Select(w =>
        {
            var asset = assetMap.GetValueOrDefault(w.AssetId);
            var requester = w.RequesterUserId is { } r ? userMap.GetValueOrDefault(r) : null;
            var parts = partCost.GetValueOrDefault(w.Id);
            return Row(w.OrderNo, w.Type.DisplayName(), w.Status.DisplayName(), w.Priority.DisplayName(),
                asset?.AssetNo, asset?.Name,
                asset is null ? null : categoryNames.GetValueOrDefault(asset.CategoryId),
                asset is null ? null : hierarchy.PathOf(asset.LocationId),
                w.Title, w.Description, requester?.DisplayName,
                requester?.DepartmentId is { } d ? departmentNames.GetValueOrDefault(d) : null,
                w.AssigneeUserId is { } a ? userMap.GetValueOrDefault(a)?.DisplayName : null,
                Local(w.CreatedAt), Local(w.ResponseDueAt), Local(w.RespondedAt),
                YesNo(WorkOrderRules.IsResponseOverdue(w, now)), Local(w.ResolveDueAt), Local(w.StartedAt),
                Local(w.CompletedAt), Local(w.ClosedAt), YesNo(WorkOrderRules.IsResolveOverdue(w, now)),
                w.LaborHours, w.FaultCause, w.Resolution, w.ReworkCount,
                w.VendorId is { } v ? vendorNames.GetValueOrDefault(v) : null,
                parts, w.VendorCost, parts + w.VendorCost, w.CancelReason);
        }).ToList();

        return new SheetData("工單明細",
        [
            "單號", "類型", "狀態", "優先級", "資產編號", "資產名稱", "類別", "地點", "標題", "描述", "申請人",
            "申請部門", "負責人", "建立時間", "回應期限", "回應時間", "回應逾時", "完工期限", "開始時間",
            "完工時間", "結案時間", "完工逾時", "工時", "故障原因", "處理方式", "退回次數", "外送廠商",
            "用料成本", "外送費用", "總成本", "取消原因"
        ], rows);
    }

    private SheetData MaintenanceSheet(ReportRange range)
    {
        var assetMap = assets.GetAll().ToDictionary(a => a.Id);
        var planMap = plans.GetAll().ToDictionary(p => p.Id);
        var userNames = users.GetAll().ToDictionary(u => u.Id, u => u.DisplayName);
        var now = time.UtcNow;

        var rows = workOrders.Query(w => w.Type == WorkOrderType.Preventive)
            .Where(w => InRange(w.CreatedAt, range)).OrderBy(w => w.CreatedAt).Select(w =>
            {
                var asset = assetMap.GetValueOrDefault(w.AssetId);
                var plan = w.MaintenancePlanId is { } p ? planMap.GetValueOrDefault(p) : null;
                var checklist = string.Join("；", w.Checklist.Select(c =>
                    $"{(c.IsChecked ? "✓" : "✗")} {c.Text}{(c.Note is null ? "" : $"（{c.Note}）")}"));
                return Row(w.OrderNo, plan?.Name, plan?.Cycle.DisplayName(), w.MaintenanceCycleKey,
                    asset?.AssetNo, asset?.Name, w.Status.DisplayName(),
                    w.AssigneeUserId is { } a ? userNames.GetValueOrDefault(a) : null,
                    Local(w.CreatedAt), Local(w.ResolveDueAt), Local(w.CompletedAt),
                    YesNo(WorkOrderRules.IsResolveOverdue(w, now)),
                    w.Checklist.Count(c => c.IsChecked), w.Checklist.Count, checklist, w.LaborHours,
                    w.FaultCause, w.Resolution);
            }).ToList();

        return new SheetData("保養紀錄",
        [
            "單號", "保養計畫", "週期", "本期", "資產編號", "資產名稱", "狀態", "負責人", "產生時間", "完工期限",
            "完工時間", "是否逾期", "已勾選項數", "檢查項數", "檢查明細", "工時", "保養狀況", "處理內容"
        ], rows);
    }

    private SheetData PartSheet(ReportRange range)
    {
        var partMap = parts.GetAll().ToDictionary(p => p.Id);
        var orderNos = workOrders.GetAll().ToDictionary(w => w.Id, w => w.OrderNo);
        var userNames = users.GetAll().ToDictionary(u => u.Id, u => u.DisplayName);

        var rows = transactions.GetAll().Where(t => InRange(t.OccurredAt, range)).OrderBy(t => t.OccurredAt)
            .Select(t =>
            {
                var part = partMap.GetValueOrDefault(t.PartId);
                return Row(Local(t.OccurredAt), part?.PartNo, part?.Name, t.Type.DisplayName(), t.Quantity,
                    part?.Unit, t.BalanceAfter, t.UnitCost, Math.Abs(t.Quantity) * t.UnitCost,
                    t.WorkOrderId is { } w ? orderNos.GetValueOrDefault(w) : null,
                    userNames.GetValueOrDefault(t.OperatorUserId), t.Note);
            }).ToList();

        return new SheetData("零件異動",
            ["時間", "料號", "零件名稱", "類型", "數量", "單位", "異動後庫存", "單價", "金額", "工單", "經手人", "備註"],
            rows);
    }

    /// <summary>用料成本依領用時間、外送費用依回廠日歸屬期間（定義同儀表板）。</summary>
    private SheetData[] CostSheets(ReportRange range)
    {
        var assetMap = assets.GetAll().ToDictionary(a => a.Id);
        var partCost = orderParts.GetAll().Where(p => InRange(p.IssuedAt, range))
            .GroupBy(p => p.WorkOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Quantity * p.UnitCost));

        var items = workOrders.GetAll().Select(w =>
            {
                var vendorCost = WorkOrderRules.VendorCostDate(w) is { } date && date >= range.From && date <= range.To
                    ? w.VendorCost
                    : 0;
                return (Order: w, Parts: partCost.GetValueOrDefault(w.Id), Vendor: vendorCost);
            })
            .Where(x => x.Parts + x.Vendor > 0)
            .OrderBy(x => x.Order.CreatedAt).ToList();

        var detail = items.Select(x =>
        {
            var asset = assetMap.GetValueOrDefault(x.Order.AssetId);
            return Row(x.Order.OrderNo, x.Order.Type.DisplayName(), x.Order.Status.DisplayName(), asset?.AssetNo,
                asset?.Name, x.Order.Title, Local(x.Order.CreatedAt), x.Order.LaborHours, x.Parts, x.Vendor,
                x.Parts + x.Vendor);
        }).ToList();

        var byAsset = items.GroupBy(x => x.Order.AssetId)
            .OrderByDescending(g => g.Sum(x => x.Parts + x.Vendor))
            .Select(g =>
            {
                var asset = assetMap.GetValueOrDefault(g.Key);
                return Row(asset?.AssetNo, asset?.Name, g.Count(), g.Sum(x => x.Parts), g.Sum(x => x.Vendor),
                    g.Sum(x => x.Parts + x.Vendor));
            }).ToList();

        return
        [
            new SheetData("維修成本明細",
            [
                "單號", "類型", "狀態", "資產編號", "資產名稱", "標題", "建立時間", "工時", "用料成本", "外送費用", "合計"
            ], detail),
            new SheetData("各資產彙總", ["資產編號", "資產名稱", "工單數", "用料成本", "外送費用", "合計"], byAsset)
        ];
    }

    private static IReadOnlyList<object?> Row(params object?[] values) => values;

    private static string YesNo(bool value) => value ? "是" : "否";

    private DateTime? Local(DateTime? utc) => utc is { } value ? time.ToLocal(value) : null;

    private bool InRange(DateTime utc, ReportRange range)
    {
        var date = DateOnly.FromDateTime(time.ToLocal(utc));
        return date >= range.From && date <= range.To;
    }
}
