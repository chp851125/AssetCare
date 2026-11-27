using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;

namespace AssetCare.Application;

/// <summary>工單的資料擁有權規則。</summary>
public static class WorkOrderAccess
{
    /// <summary>完整檢視：員工看全部，申請人看自己的。</summary>
    public static bool CanView(WorkOrder order, ICurrentUser user) =>
        user.IsStaff() || (user.UserId is { } id && order.RequesterUserId == id);

    /// <summary>處理（開始、待料、完工、領料、外送）：主管以上，或被指派的技術員。</summary>
    public static bool CanProcess(WorkOrder order, ICurrentUser user) =>
        user.IsManager() || (user.Role == UserRole.Technician && order.AssigneeUserId == user.UserId);

    /// <summary>驗收、退回與取消：申請人本人，或主管以上代為執行。</summary>
    public static bool CanVerify(WorkOrder order, ICurrentUser user) =>
        user.IsManager() || (user.UserId is { } id && order.RequesterUserId == id);
}

public static class WorkOrderLinks
{
    public static string Details(Guid id) => $"/WorkOrders/Details/{id}";
}
