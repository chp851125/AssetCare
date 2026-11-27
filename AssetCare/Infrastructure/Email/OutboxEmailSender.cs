using System.Net.Mail;
using System.Text;
using AssetCare.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace AssetCare.Infrastructure.Email;

public class EmailOptions
{
    /// <summary>相對於 ContentRoot。</summary>
    public string OutboxDirectory { get; set; } = "outbox";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "";
}

/// <summary>以寫入 outbox 資料夾的 .eml 檔模擬寄信。</summary>
public class OutboxEmailSender(
    IOptions<EmailOptions> options,
    IConfiguration configuration,
    IHostEnvironment environment,
    ITimeProvider time) : IEmailSender
{
    public string Send(string toEmail, string toName, string subject, string body, string? linkPath)
    {
        var settings = options.Value;
        // MailAddress 會拒絕格式錯誤的位址，避免換行字元混入標頭。
        var from = new MailAddress(settings.From);
        var to = new MailAddress(toEmail);

        var text = body;
        if (linkPath is not null)
            text += $"\r\n\r\n查看詳情：{configuration["App:PublicBaseUrl"]?.TrimEnd('/')}{linkPath}";

        var now = time.UtcNow;
        var content = new StringBuilder()
            .Append("From: ").Append(Encode(settings.FromName)).Append(" <").Append(from.Address).Append(">\r\n")
            .Append("To: ").Append(Encode(toName)).Append(" <").Append(to.Address).Append(">\r\n")
            .Append("Subject: ").Append(Encode(subject)).Append("\r\n")
            .Append("Date: ").Append(new DateTimeOffset(now, TimeSpan.Zero).ToString("r")).Append("\r\n")
            .Append("MIME-Version: 1.0\r\n")
            .Append("Content-Type: text/plain; charset=utf-8\r\n")
            .Append("Content-Transfer-Encoding: base64\r\n\r\n")
            .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(text), Base64FormattingOptions.InsertLineBreaks))
            .Append("\r\n")
            .ToString();

        var directory = Path.GetFullPath(settings.OutboxDirectory, environment.ContentRootPath);
        Directory.CreateDirectory(directory);
        var fileName = $"{now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}.eml";
        File.WriteAllText(Path.Combine(directory, fileName), content, Encoding.ASCII);
        return fileName;
    }

    /// <summary>RFC 2047 編碼字；分段使每行不超過長度限制，且不切斷字元。</summary>
    private static string Encode(string value)
    {
        var words = new List<string>();
        var chunk = new StringBuilder();
        void Flush()
        {
            if (chunk.Length == 0) return;
            words.Add($"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(chunk.ToString()))}?=");
            chunk.Clear();
        }
        foreach (var rune in value.EnumerateRunes())
        {
            if (Rune.IsControl(rune)) continue;
            chunk.Append(rune.ToString());
            if (Encoding.UTF8.GetByteCount(chunk.ToString()) >= 36) Flush();
        }
        Flush();
        return string.Join("\r\n ", words);
    }
}
