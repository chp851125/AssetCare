using AssetCare.Domain.Enums;

namespace AssetCare.Application.Dtos;

public record AttachmentItem(Guid Id, string FileName, string? Caption, string ContentType, long SizeBytes,
    DateTime UploadedAt, string UploadedByName, bool CanDelete)
{
    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
}

public record DeletedAttachment(AttachmentOwnerType OwnerType, Guid OwnerId);

public record UploadRules(IReadOnlyList<string> Extensions, int MaxSizeMb);
