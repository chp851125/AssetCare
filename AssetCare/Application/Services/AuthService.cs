using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace AssetCare.Application.Services;

public class AuthService(
    IRepository<User> users,
    IRepository<AppSetting> settings,
    IPasswordHasher<User> hasher,
    IAuditService audit,
    ITimeProvider time) : IAuthService
{
    // 帳號不存在、停用、密碼錯誤一律回覆相同訊息，避免帳號列舉。
    private const string InvalidMessage = "帳號或密碼錯誤，或帳號已停用。";

    public LoginResult ValidateCredentials(string userName, string password)
    {
        var name = userName.Trim();
        var user = users.GetAll().FirstOrDefault(u => string.Equals(u.UserName, name, StringComparison.OrdinalIgnoreCase));

        if (user is null || !user.IsActive)
        {
            audit.Record(AuditActions.LoginFailed, nameof(User), user?.Id, name, "帳號不存在或已停用", user?.Id, name);
            return new LoginResult(null, InvalidMessage);
        }

        var now = time.UtcNow;
        if (user.LockoutEndAt > now)
        {
            audit.Record(AuditActions.LoginFailed, nameof(User), user.Id, user.UserName, "帳號鎖定中", user.Id, user.UserName);
            return new LoginResult(null, LockedMessage(user.LockoutEndAt.Value));
        }

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.AccessFailedCount++;
            var locked = user.AccessFailedCount >= GetInt(SettingKeys.MaxFailedLogins, 5);
            if (locked)
            {
                user.LockoutEndAt = now.AddMinutes(GetInt(SettingKeys.LockoutMinutes, 15));
                user.AccessFailedCount = 0;
            }
            users.Update(user);
            audit.Record(AuditActions.LoginFailed, nameof(User), user.Id, user.UserName,
                locked ? "密碼錯誤次數過多，帳號已鎖定" : "密碼錯誤", user.Id, user.UserName);
            return new LoginResult(null, locked ? LockedMessage(user.LockoutEndAt!.Value) : InvalidMessage);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, password);
        user.AccessFailedCount = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = now;
        users.Update(user);
        audit.Record(AuditActions.Login, nameof(User), user.Id, user.UserName, null, user.Id, user.UserName);
        return new LoginResult(ToSignInUser(user), null);
    }

    public bool IsSessionValid(Guid userId, string securityStamp) =>
        users.GetById(userId) is { IsActive: true } user && user.SecurityStamp == securityStamp;

    public SignInUser GetSignInUser(Guid userId)
    {
        var user = users.GetById(userId) ?? throw new NotFoundException("找不到使用者。");
        if (!user.IsActive) throw new BusinessRuleException("此帳號已停用。");
        audit.Record(AuditActions.QuickSwitch, nameof(User), user.Id, user.UserName, "開發環境快速切換使用者", user.Id, user.UserName);
        return ToSignInUser(user);
    }

    public void RecordLogout() => audit.Record(AuditActions.Logout, nameof(User));

    private string LockedMessage(DateTime untilUtc) =>
        $"登入失敗次數過多，帳號已鎖定至 {time.ToLocal(untilUtc):HH:mm}。";

    private int GetInt(string key, int fallback) =>
        int.TryParse(settings.GetAll().FirstOrDefault(s => s.Key == key)?.Value, out var v) && v > 0 ? v : fallback;

    private static SignInUser ToSignInUser(User u) => new(u.Id, u.UserName, u.DisplayName, u.Role, u.SecurityStamp);
}
