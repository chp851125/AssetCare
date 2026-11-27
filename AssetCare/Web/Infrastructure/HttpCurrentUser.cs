using System.Security.Claims;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Enums;

namespace AssetCare.Web.Infrastructure;

public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string UserName => Principal?.Identity?.Name ?? "（匿名）";

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    public string IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "-";
}
