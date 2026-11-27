using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Entities;

public class User : BaseEntity
{
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    /// <summary>密碼、角色或啟用狀態變更時更換，使既有登入 Cookie 失效。</summary>
    public string SecurityStamp { get; set; } = "";
}
