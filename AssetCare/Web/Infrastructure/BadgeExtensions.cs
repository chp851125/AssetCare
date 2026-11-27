using AssetCare.Domain.Enums;

namespace AssetCare.Web.Infrastructure;

/// <summary>狀態標籤的 Bootstrap 樣式，搭配 &lt;span class="badge @value.BadgeClass()"&gt;。</summary>
public static class BadgeExtensions
{
    public static string BadgeClass(this AssetStatus status) => status switch
    {
        AssetStatus.InUse => "text-bg-success",
        AssetStatus.Idle => "text-bg-secondary",
        AssetStatus.UnderRepair => "text-bg-warning",
        _ => "text-bg-dark"
    };

    public static string BadgeClass(this WarrantyState state) => state switch
    {
        WarrantyState.Active => "text-bg-success",
        WarrantyState.Expiring => "text-bg-warning",
        WarrantyState.Expired => "text-bg-danger",
        _ => "text-bg-light border"
    };

    public static string BadgeClass(this MaintenanceDueState state) => state switch
    {
        MaintenanceDueState.Overdue => "text-bg-danger",
        MaintenanceDueState.DueSoon => "text-bg-warning",
        MaintenanceDueState.Normal => "text-bg-success",
        _ => "text-bg-secondary"
    };

    public static string BadgeClass(this Priority priority) => priority switch
    {
        Priority.Urgent => "text-bg-danger",
        Priority.High => "text-bg-warning",
        Priority.Normal => "text-bg-light border",
        _ => "text-bg-light border text-secondary"
    };

    public static string BadgeClass(this WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Open => "text-bg-info",
        WorkOrderStatus.Assigned or WorkOrderStatus.InProgress => "text-bg-primary",
        WorkOrderStatus.WaitingParts or WorkOrderStatus.Completed => "text-bg-warning",
        WorkOrderStatus.Closed => "text-bg-success",
        _ => "text-bg-secondary"
    };
}
