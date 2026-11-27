namespace AssetCare.Application.Interfaces;

public interface IEmailSender
{
    /// <summary>寄送郵件並回傳郵件檔名；失敗時丟出例外。linkPath 為站內相對路徑。</summary>
    string Send(string toEmail, string toName, string subject, string body, string? linkPath);
}
