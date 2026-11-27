using AssetCare.Application.Interfaces;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;

namespace AssetCare.Application.Services;

public class DataResetService(IDataStore store, ICurrentUser currentUser, IAuditService audit) : IDataResetService
{
    public void ResetToSampleData()
    {
        if (currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("只有系統管理員可以重置資料。");

        var actorId = currentUser.UserId;
        var actorName = currentUser.UserName;
        store.ResetToSeed();
        // 重置後才寫入，稽核紀錄才會保留在新資料中。
        audit.Record(AuditActions.ResetData, "System", detail: "重置為範例資料", actorUserId: actorId, actorName: actorName);
    }
}
