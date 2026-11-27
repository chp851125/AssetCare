using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;

namespace AssetCare.Application.Services;

public class AuditService(IRepository<AuditLog> logs, ICurrentUser currentUser, ITimeProvider time) : IAuditService
{
    public void Record(string action, string entityType, Guid? entityId = null, string? entityName = null,
        string? detail = null, Guid? actorUserId = null, string? actorName = null)
    {
        logs.Add(new AuditLog
        {
            UserId = actorUserId ?? currentUser.UserId,
            UserName = actorName ?? currentUser.UserName,
            OccurredAt = time.UtcNow,
            IpAddress = currentUser.IpAddress,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            Detail = detail
        });
    }
}
