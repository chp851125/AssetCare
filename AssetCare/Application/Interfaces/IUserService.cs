using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

public interface IUserService
{
    /// <summary>僅供 Development 快速切換使用者下拉。</summary>
    IReadOnlyList<UserOption> GetActiveUserOptions();
    UserProfile GetCurrentProfile();

    /// <summary>以下使用者管理操作僅限系統管理員。</summary>
    PagedResult<UserListItem> Search(UserQuery query);
    IReadOnlyList<Option> GetDepartmentOptions();
    int GetPasswordMinLength();
    UserEditData GetForEdit(Guid id);
    void Create(string? userName, string? password, UserInput input);
    void Update(Guid id, int version, UserInput input);
    /// <summary>停用或啟用。回傳需要人工後續處理的提醒（例如仍有未結案工單）。</summary>
    string? SetActive(Guid id, int version, bool isActive);
    void ResetPassword(Guid id, int version, string? newPassword);
    void Unlock(Guid id, int version);
}
