using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

public interface IAuthService
{
    LoginResult ValidateCredentials(string userName, string password);
    /// <summary>Cookie 驗證時確認帳號仍啟用且安全戳記未變更。</summary>
    bool IsSessionValid(Guid userId, string securityStamp);
    /// <summary>僅供 Development 快速切換使用者。</summary>
    SignInUser GetSignInUser(Guid userId);
    void RecordLogout();
}
