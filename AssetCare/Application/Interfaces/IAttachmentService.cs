using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

public interface IAttachmentService
{
    UploadRules GetUploadRules();
    /// <summary>僅限員工。</summary>
    void UploadForAsset(Guid assetId, IReadOnlyList<UploadedFile> files, string? caption);
    /// <summary>僅限員工。</summary>
    FileDownload Open(Guid id);
    /// <summary>主管以上或上傳者本人。回傳所屬資產 Id。</summary>
    Guid Delete(Guid id);
}
