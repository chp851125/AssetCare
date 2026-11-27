using AssetCare.Domain.Enums;

namespace AssetCare.Application.Interfaces;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string UserName { get; }
    UserRole? Role { get; }
    string IpAddress { get; }
}
