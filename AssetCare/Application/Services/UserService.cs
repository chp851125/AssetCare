using System.Net.Mail;
using System.Text.RegularExpressions;
using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;
using Microsoft.AspNetCore.Identity;

namespace AssetCare.Application.Services;

public partial class UserService(
    IRepository<User> users,
    IRepository<Department> departments,
    IRepository<WorkOrder> workOrders,
    IRepository<MaintenancePlan> plans,
    IRepository<AppSetting> settings,
    IPasswordHasher<User> hasher,
    IBusinessLock businessLock,
    IAuditService audit,
    ITimeProvider time,
    ICurrentUser currentUser) : IUserService
{
    private const int PageSize = 10;

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{2,29}$")]
    private static partial Regex UserNamePattern();

    public IReadOnlyList<UserOption> GetActiveUserOptions() =>
        users.Query(u => u.IsActive)
            .OrderByDescending(u => u.Role).ThenBy(u => u.UserName)
            .Select(u => new UserOption(u.Id, u.UserName, u.DisplayName, u.Role))
            .ToList();

    public UserProfile GetCurrentProfile()
    {
        var user = users.GetById(currentUser.RequireUserId()) ?? throw new NotFoundException("找不到使用者。");
        var department = user.DepartmentId is { } id ? departments.GetById(id)?.Name : null;
        return new UserProfile(user.UserName, user.DisplayName, user.Email, user.Phone, user.Role, department, user.LastLoginAt);
    }

    public PagedResult<UserListItem> Search(UserQuery query)
    {
        currentUser.RequireAdmin();
        var departmentNames = departments.GetAll().ToDictionary(d => d.Id, d => d.Name);
        var now = time.UtcNow;

        IEnumerable<User> source = users.GetAll();
        if (query.Keyword?.Trim() is { Length: > 0 } keyword)
            source = source.Where(u => new[] { u.UserName, u.DisplayName, u.Email }
                .Any(v => v.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        if (query.Role is { } role) source = source.Where(u => u.Role == role);
        if (query.DepartmentId is { } departmentId) source = source.Where(u => u.DepartmentId == departmentId);
        if (query.IsActive is { } active) source = source.Where(u => u.IsActive == active);

        var items = source.OrderByDescending(u => u.IsActive).ThenBy(u => u.UserName, StringComparer.Ordinal)
            .Select(u => new UserListItem(u.Id, u.UserName, u.DisplayName, u.Email, u.Role,
                u.DepartmentId is { } d ? departmentNames.GetValueOrDefault(d) : null,
                u.IsActive, u.LockoutEndAt > now, u.LastLoginAt))
            .ToList();
        return PagedResult<UserListItem>.From(items, query.Page, PageSize);
    }

    public IReadOnlyList<Option> GetDepartmentOptions()
    {
        currentUser.RequireAdmin();
        return departments.Query(d => d.IsActive).OrderBy(d => d.Code).Select(d => new Option(d.Id, d.Name)).ToList();
    }

    public int GetPasswordMinLength() => settings.GetInt(SettingKeys.PasswordMinLength, 8);

    public UserEditData GetForEdit(Guid id)
    {
        currentUser.RequireAdmin();
        var u = Find(id);
        return new UserEditData(u.Id, u.Version, u.UserName, u.IsActive,
            u.LockoutEndAt > time.UtcNow ? u.LockoutEndAt : null, u.Id == currentUser.UserId,
            new UserInput(u.DisplayName, u.Email, u.Phone, u.Role, u.DepartmentId));
    }

    public void Create(string? userName, string? password, UserInput input)
    {
        currentUser.RequireAdmin();
        var name = InputText.Required(userName, "帳號", 30).ToLowerInvariant();
        if (!UserNamePattern().IsMatch(name))
            throw new BusinessRuleException("帳號須為 3 到 30 碼英文、數字、點、底線或連字號。");

        businessLock.Run(() =>
        {
            if (users.GetAll().Any(u => string.Equals(u.UserName, name, StringComparison.OrdinalIgnoreCase)))
                throw new BusinessRuleException($"帳號 {name} 已存在。");
            var user = new User { UserName = name, IsActive = true };
            Apply(user, input);
            SetPassword(user, password);
            users.Add(user);
            audit.Record(AuditActions.Create, nameof(User), user.Id, user.UserName,
                $"新增使用者（{user.Role.DisplayName()}）");
        });
    }

    public void Update(Guid id, int version, UserInput input)
    {
        currentUser.RequireAdmin();
        businessLock.Run(() =>
        {
            var user = Find(id);
            var oldRole = user.Role;
            if (input.Role != oldRole)
            {
                if (user.Id == currentUser.UserId) throw new BusinessRuleException("不可變更自己的角色。");
                EnsureAnotherAdmin(user);
                // 工單負責人必須是技術員。
                if (oldRole == UserRole.Technician && ActiveOrderCount(id) is var count and > 0)
                    throw new BusinessRuleException($"此技術員尚有 {count} 張未結案工單，請先改派後再變更角色。");
            }
            Apply(user, input);
            if (user.Role != oldRole) user.SecurityStamp = NewStamp();
            user.Version = version;
            users.Update(user);
            audit.Record(AuditActions.Update, nameof(User), user.Id, user.UserName,
                user.Role == oldRole
                    ? "修改使用者資料"
                    : $"角色由{oldRole.DisplayName()}改為{user.Role.DisplayName()}");
        });
    }

    public string? SetActive(Guid id, int version, bool isActive)
    {
        currentUser.RequireAdmin();
        return businessLock.Run(() =>
        {
            var user = Find(id);
            if (user.IsActive == isActive)
                throw new BusinessRuleException(isActive ? "此帳號已是啟用狀態。" : "此帳號已停用。");
            if (!isActive)
            {
                if (user.Id == currentUser.UserId) throw new BusinessRuleException("不可停用自己的帳號。");
                EnsureAnotherAdmin(user);
            }
            user.IsActive = isActive;
            user.AccessFailedCount = 0;
            user.LockoutEndAt = null;
            user.SecurityStamp = NewStamp();
            user.Version = version;
            users.Update(user);
            audit.Record(isActive ? AuditActions.Activate : AuditActions.Deactivate, nameof(User),
                user.Id, user.UserName, isActive ? "啟用帳號" : "停用帳號");

            if (isActive) return null;
            // 停用（例如離職）不阻擋，但提醒後續交接。
            var orders = ActiveOrderCount(id);
            var planCount = plans.Query(p => p.AssigneeUserId == id && p.IsActive).Count;
            return orders + planCount == 0
                ? null
                : $"此使用者仍負責 {orders} 張未結案工單與 {planCount} 個保養計畫，請安排交接。";
        });
    }

    public void ResetPassword(Guid id, int version, string? newPassword)
    {
        currentUser.RequireAdmin();
        businessLock.Run(() =>
        {
            var user = Find(id);
            SetPassword(user, newPassword);
            user.AccessFailedCount = 0;
            user.LockoutEndAt = null;
            user.Version = version;
            users.Update(user);
            audit.Record(AuditActions.ResetPassword, nameof(User), user.Id, user.UserName, "管理員重設密碼");
        });
    }

    public void Unlock(Guid id, int version)
    {
        currentUser.RequireAdmin();
        businessLock.Run(() =>
        {
            var user = Find(id);
            user.AccessFailedCount = 0;
            user.LockoutEndAt = null;
            user.Version = version;
            users.Update(user);
            audit.Record(AuditActions.Unlock, nameof(User), user.Id, user.UserName, "解除登入鎖定");
        });
    }

    private User Find(Guid id) => users.GetById(id) ?? throw new NotFoundException("找不到使用者。");

    private int ActiveOrderCount(Guid userId) => workOrders.Query(w => w.AssigneeUserId == userId)
        .Count(w => WorkOrderStateMachine.IsActive(w.Status));

    /// <summary>系統至少要保留一位啟用中的管理員。</summary>
    private void EnsureAnotherAdmin(User target)
    {
        if (target is { Role: UserRole.Admin, IsActive: true } &&
            users.Query(u => u.Role == UserRole.Admin && u.IsActive && u.Id != target.Id).Count == 0)
            throw new BusinessRuleException("系統至少需保留一位啟用中的系統管理員。");
    }

    /// <summary>驗證密碼原則、寫入雜湊並更換安全戳記（使既有登入失效）。</summary>
    private void SetPassword(User user, string? password)
    {
        if (string.IsNullOrEmpty(password)) throw new BusinessRuleException("請輸入密碼。");
        if (PasswordRules.Validate(password, user.UserName, GetPasswordMinLength()) is { } error)
            throw new BusinessRuleException(error);
        user.PasswordHash = hasher.HashPassword(user, password);
        user.SecurityStamp = NewStamp();
    }

    private void Apply(User user, UserInput input)
    {
        var email = InputText.Required(input.Email, "Email", 100);
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new BusinessRuleException("Email 格式不正確。");
        if (!Enum.IsDefined(input.Role)) throw new BusinessRuleException("請選擇角色。");
        // 僅在變更部門時要求啟用中，原部門停用不阻擋其他修改。
        if (input.DepartmentId is { } d && d != user.DepartmentId && departments.GetById(d) is not { IsActive: true })
            throw new BusinessRuleException("請選擇啟用中的部門。");

        user.DisplayName = InputText.Required(input.DisplayName, "姓名", 50);
        user.Email = email;
        user.Phone = InputText.Optional(input.Phone, "電話", 30);
        user.Role = input.Role;
        user.DepartmentId = input.DepartmentId;
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
