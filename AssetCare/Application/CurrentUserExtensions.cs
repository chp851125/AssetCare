using AssetCare.Application.Interfaces;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;

namespace AssetCare.Application;

/// <summary>Service 層的角色檢查。</summary>
public static class CurrentUserExtensions
{
    /// <summary>主管或管理員。</summary>
    public static bool IsManager(this ICurrentUser user) => user.Role is UserRole.Supervisor or UserRole.Admin;

    /// <summary>技術員、主管或管理員。</summary>
    public static bool IsStaff(this ICurrentUser user) => user.Role is UserRole.Technician || user.IsManager();

    public static Guid RequireUserId(this ICurrentUser user) => user.UserId ?? throw new ForbiddenException();

    public static void RequireManager(this ICurrentUser user)
    {
        if (!user.IsManager()) throw new ForbiddenException();
    }

    public static void RequireStaff(this ICurrentUser user)
    {
        if (!user.IsStaff()) throw new ForbiddenException();
    }

    public static void RequireAdmin(this ICurrentUser user)
    {
        if (user.Role != UserRole.Admin) throw new ForbiddenException();
    }
}
