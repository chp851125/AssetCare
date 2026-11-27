using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Rules;

public static class WorkOrderRules
{
    public static string OrderNoPrefix(WorkOrderType type, DateOnly localDate) =>
        $"{(type == WorkOrderType.Repair ? "WO" : "PM")}-{localDate:yyyyMMdd}-";

    /// <summary>同前綴現有最大序號 +1。</summary>
    public static string NextOrderNo(string prefix, IEnumerable<string> existing)
    {
        var max = existing.Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), out var number) ? number : 0)
            .DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:0000}";
    }

    /// <summary>已標示逾時，或依目前時間判斷逾時。取消的工單只保留既有標示。</summary>
    public static bool IsResponseOverdue(WorkOrder order, DateTime nowUtc) => order.IsResponseOverdue ||
        (order.Status != WorkOrderStatus.Cancelled &&
         SlaRules.IsOverdue(order.ResponseDueAt, order.RespondedAt, nowUtc));

    public static bool IsResolveOverdue(WorkOrder order, DateTime nowUtc) => order.IsResolveOverdue ||
        (order.Status != WorkOrderStatus.Cancelled &&
         SlaRules.IsOverdue(order.ResolveDueAt, order.CompletedAt, nowUtc));

    /// <summary>外送費用的歸屬日期：回廠日；再次外送尚未送回時以外送日計。</summary>
    public static DateOnly? VendorCostDate(WorkOrder order) =>
        order.VendorCost > 0 ? order.ReturnedDate ?? order.SentOutDate : null;

    /// <summary>
    /// 是否列入「近期重複報修」：未取消的報修工單，且建立時間距今不超過指定天數（剛好滿天數仍算）。
    /// </summary>
    public static bool IsRecentRepair(WorkOrder order, DateTime nowUtc, int days) =>
        order.Type == WorkOrderType.Repair && order.Status != WorkOrderStatus.Cancelled &&
        order.CreatedAt >= nowUtc.AddDays(-days);
}
