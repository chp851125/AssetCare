using System.Security.Claims;
using AssetCare.Domain.Enums;

namespace AssetCare.Web.Infrastructure;

/// <summary>供 [Authorize(Roles = ...)] 使用的角色組合。</summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    /// <summary>主管與管理員。</summary>
    public const string Managers = nameof(UserRole.Supervisor) + "," + nameof(UserRole.Admin);
    /// <summary>技術員、主管與管理員。</summary>
    public const string Staff = nameof(UserRole.Technician) + "," + Managers;
}

/// <summary>檢視中決定顯示哪些操作；伺服器端授權仍由 [Authorize] 與 Service 把關。</summary>
public static class PrincipalExtensions
{
    public static bool IsManager(this ClaimsPrincipal user) =>
        user.IsInRole(nameof(UserRole.Supervisor)) || user.IsInRole(nameof(UserRole.Admin));

    public static bool IsStaff(this ClaimsPrincipal user) =>
        user.IsInRole(nameof(UserRole.Technician)) || user.IsManager();
}
