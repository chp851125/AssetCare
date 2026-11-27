namespace AssetCare.Application.Interfaces;

/// <summary>由背景服務定期呼叫，不需登入身分。</summary>
public interface IReminderService
{
    /// <summary>發送保固即將到期與保養即將到期通知；同一到期日對同一人只通知一次。</summary>
    void SendDueReminders();
}
