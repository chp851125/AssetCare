using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Rules;

/// <summary>工單狀態轉移的唯一定義來源。</summary>
public static class WorkOrderStateMachine
{
    private static readonly Dictionary<WorkOrderStatus, WorkOrderStatus[]> Transitions = new()
    {
        [WorkOrderStatus.Open] = [WorkOrderStatus.Assigned, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.Assigned] = [WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.InProgress] = [WorkOrderStatus.WaitingParts, WorkOrderStatus.Completed],
        [WorkOrderStatus.WaitingParts] = [WorkOrderStatus.InProgress],
        [WorkOrderStatus.Completed] = [WorkOrderStatus.Closed, WorkOrderStatus.InProgress],
        [WorkOrderStatus.Closed] = [],
        [WorkOrderStatus.Cancelled] = []
    };

    public static bool CanTransition(WorkOrderStatus from, WorkOrderStatus to) => Transitions[from].Contains(to);

    /// <summary>尚未結案也未取消。</summary>
    public static bool IsActive(WorkOrderStatus status) =>
        status is not (WorkOrderStatus.Closed or WorkOrderStatus.Cancelled);
}
