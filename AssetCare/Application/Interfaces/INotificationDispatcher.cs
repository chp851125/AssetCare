using AssetCare.Domain.Enums;

namespace AssetCare.Application.Interfaces;

/// <summary>系統內部發送通知（站內通知與模擬 Email）。不提供給 Controller。</summary>
public interface INotificationDispatcher
{
    /// <summary>啟用中的主管與管理員。</summary>
    IReadOnlyList<Guid> ManagerIds();

    /// <summary>發給指定使用者；自動略過重複、停用帳號與目前操作者本人。發送失敗只記錄，不中斷呼叫端。</summary>
    void Send(IEnumerable<Guid?> userIds, NotificationType type, string title, string message, string linkUrl);
}
