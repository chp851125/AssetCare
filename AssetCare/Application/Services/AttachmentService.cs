using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class AttachmentService(
    IRepository<Attachment> attachments,
    IRepository<Asset> assets,
    IRepository<WorkOrder> workOrders,
    IRepository<AppSetting> settings,
    IFileStorage storage,
    ICurrentUser currentUser,
    IAuditService audit) : IAttachmentService
{
    public const int MaxFilesPerUpload = 5;

    /// <summary>已通過類型、大小與內容檢查的上傳檔。</summary>
    internal record AcceptedFile(UploadedFile Source, string FileName, string Extension, string ContentType);

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

    /// <summary>供系統參數驗證允許的副檔名。</summary>
    internal static bool IsKnownExtension(string extension) => KnownTypes.ContainsKey(extension);

    internal static string KnownExtensionList => string.Join("、", KnownTypes.Keys);

    public UploadRules GetUploadRules()
    {
        var configured = settings.GetValue(SettingKeys.UploadAllowedExtensions) ?? "";
        var extensions = configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant()).Where(KnownTypes.ContainsKey).Distinct().ToList();
        return new UploadRules(extensions, settings.GetInt(SettingKeys.UploadMaxSizeMb, 10));
    }

    public UploadRules GetPhotoRules()
    {
        var rules = GetUploadRules();
        return rules with
        {
            Extensions = rules.Extensions
                .Where(e => KnownTypes[e].ContentType.StartsWith("image/", StringComparison.Ordinal)).ToList()
        };
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
        var accepted = files.Select(f => Check(f, rules)).ToList();

        Save(AttachmentOwnerType.Asset, assetId, accepted, note);
        foreach (var file in accepted)
            audit.Record(AuditActions.Upload, nameof(Asset), asset.Id, asset.AssetNo, $"上傳附件 {file.FileName}");
    }

    public void UploadForWorkOrder(Guid workOrderId, IReadOnlyList<UploadedFile> files, string? caption)
    {
        var userId = currentUser.RequireUserId();
        var order = workOrders.GetById(workOrderId) ?? throw new NotFoundException("找不到工單。");
        if (!currentUser.IsManager() && order.AssigneeUserId != userId && order.RequesterUserId != userId)
            throw new ForbiddenException("只有申請人、負責技術員或主管可以上傳照片。");
        if (!WorkOrderStateMachine.IsActive(order.Status))
            throw new BusinessRuleException("工單已結案或取消，無法再上傳照片。");
        if (files.Count == 0) throw new BusinessRuleException("請選擇要上傳的照片。");
        var note = InputText.Optional(caption, "照片說明", 200);

        Save(AttachmentOwnerType.WorkOrder, order.Id, CheckPhotos(files), note);
        audit.Record(AuditActions.Upload, nameof(WorkOrder), order.Id, order.OrderNo, $"上傳照片 {files.Count} 張");
    }

    /// <summary>工單照片：一次最多 5 張、僅限圖片。只做檢查，不寫入。</summary>
    internal List<AcceptedFile> CheckPhotos(IReadOnlyList<UploadedFile> files)
    {
        if (files.Count == 0) return [];
        if (files.Count > MaxFilesPerUpload)
            throw new BusinessRuleException($"一次最多上傳 {MaxFilesPerUpload} 張照片。");
        var rules = GetPhotoRules();
        return files.Select(f => Check(f, rules)).ToList();
    }

    /// <summary>寫入已通過檢查的檔案。</summary>
    internal void Save(AttachmentOwnerType ownerType, Guid ownerId, IReadOnlyList<AcceptedFile> files,
        string? caption)
    {
        foreach (var file in files)
            attachments.Add(new Attachment
            {
                OwnerType = ownerType,
                OwnerId = ownerId,
                FileName = file.FileName,
                StoredFileName = storage.Save(file.Source.Content, file.Extension),
                ContentType = file.ContentType,
                SizeBytes = file.Source.Length,
                Caption = caption,
                UploadedByUserId = currentUser.RequireUserId()
            });
    }

    public FileDownload Open(Guid id)
    {
        currentUser.RequireUserId();
        var attachment = Find(id);
        if (attachment.OwnerType == AttachmentOwnerType.Asset) currentUser.RequireStaff();
        else EnsureCanViewOrder(attachment.OwnerId);
        var stream = storage.OpenRead(attachment.StoredFileName) ?? throw new NotFoundException("附件檔案已遺失。");
        return new FileDownload(stream, attachment.ContentType, attachment.FileName);
    }

    public DeletedAttachment Delete(Guid id)
    {
        var userId = currentUser.RequireUserId();
        var attachment = Find(id);
        string entityType;
        string? ownerName;
        if (attachment.OwnerType == AttachmentOwnerType.Asset)
        {
            currentUser.RequireStaff();
            entityType = nameof(Asset);
            ownerName = assets.GetById(attachment.OwnerId)?.AssetNo;
        }
        else
        {
            var order = EnsureCanViewOrder(attachment.OwnerId);
            if (!currentUser.IsManager() && !WorkOrderStateMachine.IsActive(order.Status))
                throw new BusinessRuleException("工單已結案或取消，無法刪除照片。");
            entityType = nameof(WorkOrder);
            ownerName = order.OrderNo;
        }
        if (!currentUser.IsManager() && attachment.UploadedByUserId != userId)
            throw new ForbiddenException("只能刪除自己上傳的附件。");

        attachments.Delete(attachment);
        storage.Delete(attachment.StoredFileName);
        audit.Record(AuditActions.Delete, entityType, attachment.OwnerId, ownerName, $"刪除附件 {attachment.FileName}");
        return new DeletedAttachment(attachment.OwnerType, attachment.OwnerId);
    }

    private Attachment Find(Guid id) => attachments.GetById(id) ?? throw new NotFoundException("找不到附件。");

    private WorkOrder EnsureCanViewOrder(Guid orderId)
    {
        var order = workOrders.GetById(orderId) ?? throw new NotFoundException("找不到工單。");
        return WorkOrderAccess.CanView(order, currentUser)
            ? order
            : throw new ForbiddenException("只能存取自己工單的照片。");
    }

    private static AcceptedFile Check(UploadedFile file, UploadRules rules)
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
        return new AcceptedFile(file, name, extension, type.ContentType);
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
