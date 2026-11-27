namespace AssetCare.Application.Interfaces;

/// <summary>由背景服務定期呼叫，不需登入身分。</summary>
public interface ISlaService
{
    /// <summary>標示新逾時的工單並通知主管。回傳本次新標示的工單數。</summary>
    int ScanOverdue();
}
