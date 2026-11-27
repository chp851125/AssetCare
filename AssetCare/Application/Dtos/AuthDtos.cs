using AssetCare.Domain.Enums;

namespace AssetCare.Application.Dtos;

public record SignInUser(Guid Id, string UserName, string DisplayName, UserRole Role, string SecurityStamp);

public record LoginResult(SignInUser? User, string? ErrorMessage)
{
    public bool Succeeded => User is not null;
}

public record UserOption(Guid Id, string UserName, string DisplayName, UserRole Role);

public record UserProfile(string UserName, string DisplayName, string Email, string? Phone, UserRole Role,
    string? DepartmentName, DateTime? LastLoginAt);
