using AssetCare.Domain.Enums;

namespace AssetCare.Application.Dtos;

public class UserQuery
{
    public string? Keyword { get; set; }
    public UserRole? Role { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
}

public record UserListItem(Guid Id, string UserName, string DisplayName, string Email, UserRole Role,
    string? DepartmentName, bool IsActive, bool IsLocked, DateTime? LastLoginAt);

public record UserInput(string? DisplayName, string? Email, string? Phone, UserRole Role, Guid? DepartmentId);

public record UserEditData(Guid Id, int Version, string UserName, bool IsActive, DateTime? LockedUntil,
    bool IsSelf, UserInput Input);
