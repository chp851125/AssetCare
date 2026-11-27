using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Entities;

public class Attachment : BaseEntity
{
    public AttachmentOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    /// <summary>使用者上傳時的原始檔名（僅供顯示）。</summary>
    public string FileName { get; set; } = "";
    /// <summary>App_Data/uploads 下的實體檔名。</summary>
    public string StoredFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? Caption { get; set; }
    public Guid UploadedByUserId { get; set; }
}
