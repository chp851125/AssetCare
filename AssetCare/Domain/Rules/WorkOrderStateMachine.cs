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

    /// <summary>
    /// 各動作允許的起始狀態與結果狀態；結果為 null 表示不改變狀態。
    /// 同一組狀態轉移可能對應不同動作（「恢復處理」與「驗收退回」都回到處理中），因此必須依動作判斷。
    /// </summary>
    private static readonly Dictionary<WorkOrderAction, (WorkOrderStatus[] From, WorkOrderStatus? To)> Actions = new()
    {
        [WorkOrderAction.Assigned] = ([WorkOrderStatus.Open], WorkOrderStatus.Assigned),
        [WorkOrderAction.Accepted] = ([WorkOrderStatus.Open], WorkOrderStatus.Assigned),
        [WorkOrderAction.Started] = ([WorkOrderStatus.Assigned], WorkOrderStatus.InProgress),
        [WorkOrderAction.WaitingParts] = ([WorkOrderStatus.InProgress], WorkOrderStatus.WaitingParts),
        [WorkOrderAction.Resumed] = ([WorkOrderStatus.WaitingParts], WorkOrderStatus.InProgress),
        [WorkOrderAction.Completed] = ([WorkOrderStatus.InProgress], WorkOrderStatus.Completed),
        [WorkOrderAction.Verified] = ([WorkOrderStatus.Completed], WorkOrderStatus.Closed),
        [WorkOrderAction.Rejected] = ([WorkOrderStatus.Completed], WorkOrderStatus.InProgress),
        [WorkOrderAction.Cancelled] = ([WorkOrderStatus.Open, WorkOrderStatus.Assigned], WorkOrderStatus.Cancelled),
        [WorkOrderAction.Reassigned] =
            ([WorkOrderStatus.Assigned, WorkOrderStatus.InProgress, WorkOrderStatus.WaitingParts], null),
        [WorkOrderAction.PartIssued] = ([WorkOrderStatus.InProgress, WorkOrderStatus.WaitingParts], null),
        [WorkOrderAction.ChecklistUpdated] = ([WorkOrderStatus.InProgress, WorkOrderStatus.WaitingParts], null),
        [WorkOrderAction.SentToVendor] =
            ([WorkOrderStatus.Assigned, WorkOrderStatus.InProgress, WorkOrderStatus.WaitingParts], null),
        [WorkOrderAction.ReturnedFromVendor] =
            ([WorkOrderStatus.Assigned, WorkOrderStatus.InProgress, WorkOrderStatus.WaitingParts], null)
    };

    public static bool CanTransition(WorkOrderStatus from, WorkOrderStatus to) => Transitions[from].Contains(to);

    /// <summary>目前狀態是否允許執行該動作。留言不受狀態限制。</summary>
    public static bool Allows(WorkOrderAction action, WorkOrderStatus current) =>
        action == WorkOrderAction.Comment ||
        (Actions.TryGetValue(action, out var rule) && rule.From.Contains(current) &&
         (rule.To is not { } to || CanTransition(current, to)));

    /// <summary>動作完成後的狀態；呼叫前須先以 Allows 確認。</summary>
    public static WorkOrderStatus ResultOf(WorkOrderAction action, WorkOrderStatus current) =>
        Actions[action].To ?? current;

    /// <summary>尚未結案也未取消。</summary>
    public static bool IsActive(WorkOrderStatus status) =>
        status is not (WorkOrderStatus.Closed or WorkOrderStatus.Cancelled);
}
