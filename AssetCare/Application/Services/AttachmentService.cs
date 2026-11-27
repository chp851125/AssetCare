using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;

namespace AssetCare.Application.Services;

public class AttachmentService(
    IRepository<Attachment> attachments,
    IRepository<Asset> assets,
    IRepository<AppSetting> settings,
    IFileStorage storage,
    ICurrentUser currentUser,
    IAuditService audit) : IAttachmentService
{
    public const int MaxFilesPerUpload = 5;

    /// <summary>系統認得的類型；設定值只能從中挑選，檔案開頭需符合特徵碼。</summary>
    private static readonly Dictionary<string, (string ContentType, byte[] Signature)> KnownTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
            [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
            [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            [".webp"] = ("image/webp", "RIFF"u8.ToArray()),
            [".pdf"] = ("application/pdf", "%PDF-"u8.ToArray()),
            [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [0x50, 0x4B, 0x03, 0x04]),
            [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B, 0x03, 0x04])
        };

    public UploadRules GetUploadRules()
    {
        var configured = settings.GetValue(SettingKeys.UploadAllowedExtensions) ?? "";
        var extensions = configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant()).Where(KnownTypes.ContainsKey).Distinct().ToList();
        return new UploadRules(extensions, settings.GetInt(SettingKeys.UploadMaxSizeMb, 10));
    }

    public void UploadForAsset(Guid assetId, IReadOnlyList<UploadedFile> files, string? caption)
    {
        currentUser.RequireStaff();
        var asset = assets.GetById(assetId) ?? throw new NotFoundException("找不到資產。");
        if (files.Count == 0) throw new BusinessRuleException("請選擇要上傳的檔案。");
        if (files.Count > MaxFilesPerUpload)
            throw new BusinessRuleException($"一次最多上傳 {MaxFilesPerUpload} 個檔案。");
        var note = InputText.Optional(caption, "附件說明", 200);

        // 全部檢查通過才寫入。
        var rules = GetUploadRules();
        var accepted = files.Select(f => (File: f, Type: Check(f, rules))).ToList();

        foreach (var (file, type) in accepted)
        {
            var fileName = SafeFileName(file.FileName);
            attachments.Add(new Attachment
            {
                OwnerType = AttachmentOwnerType.Asset,
                OwnerId = assetId,
                FileName = fileName,
                StoredFileName = storage.Save(file.Content, type.Extension),
                ContentType = type.ContentType,
                SizeBytes = file.Length,
                Caption = note,
                UploadedByUserId = currentUser.RequireUserId()
            });
            audit.Record(AuditActions.Upload, nameof(Asset), asset.Id, asset.AssetNo, $"上傳附件 {fileName}");
        }
    }

    public FileDownload Open(Guid id)
    {
        currentUser.RequireStaff();
        var attachment = FindAssetAttachment(id);
        var stream = storage.OpenRead(attachment.StoredFileName) ?? throw new NotFoundException("附件檔案已遺失。");
        return new FileDownload(stream, attachment.ContentType, attachment.FileName);
    }

    public Guid Delete(Guid id)
    {
        currentUser.RequireStaff();
        var attachment = FindAssetAttachment(id);
        if (!currentUser.IsManager() && attachment.UploadedByUserId != currentUser.UserId)
            throw new ForbiddenException("只能刪除自己上傳的附件。");

        attachments.Delete(attachment);
        storage.Delete(attachment.StoredFileName);
        audit.Record(AuditActions.Delete, nameof(Asset), attachment.OwnerId,
            assets.GetById(attachment.OwnerId)?.AssetNo, $"刪除附件 {attachment.FileName}");
        return attachment.OwnerId;
    }

    /// <summary>工單附件的存取規則於階段 3 加入；在此之前一律視為不存在。</summary>
    private Attachment FindAssetAttachment(Guid id) =>
        attachments.GetById(id) is { OwnerType: AttachmentOwnerType.Asset } a
            ? a
            : throw new NotFoundException("找不到附件。");

    private static (string Extension, string ContentType) Check(UploadedFile file, UploadRules rules)
    {
        var name = SafeFileName(file.FileName);
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!rules.Extensions.Contains(extension))
            throw new BusinessRuleException($"{name}：不允許的檔案類型，僅接受 {string.Join("、", rules.Extensions)}。");
        if (file.Length <= 0) throw new BusinessRuleException($"{name}：檔案是空的。");
        if (file.Length > rules.MaxSizeMb * 1024L * 1024L)
            throw new BusinessRuleException($"{name}：超過 {rules.MaxSizeMb} MB 上限。");

        var type = KnownTypes[extension];
        var head = new byte[12];
        var read = file.Content.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        file.Content.Position = 0;
        var matches = head.AsSpan(0, read).StartsWith(type.Signature) &&
                      (extension != ".webp" || head.AsSpan(8, 4).SequenceEqual("WEBP"u8));
        if (!matches) throw new BusinessRuleException($"{name}：檔案內容與副檔名不符。");
        return (extension, type.ContentType);
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0) return "附件";
        if (name.Length <= 150) return name;
        // 過長時保留副檔名
        var extension = Path.GetExtension(name);
        return extension.Length < 20 ? name[..(150 - extension.Length)] + extension : name[..150];
    }
}
