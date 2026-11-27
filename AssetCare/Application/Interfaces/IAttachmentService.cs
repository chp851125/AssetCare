using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

public interface IAttachmentService
{
    UploadRules GetUploadRules();
    /// <summary>僅限員工。</summary>
    void UploadForAsset(Guid assetId, IReadOnlyList<UploadedFile> files, string? caption);
    /// <summary>工單照片允許的類型（僅圖片）。</summary>
    UploadRules GetPhotoRules();
    /// <summary>申請人、負責技術員或主管以上，限未結案工單。</summary>
    void UploadForWorkOrder(Guid workOrderId, IReadOnlyList<UploadedFile> files, string? caption);
    /// <summary>資產附件限員工；工單照片限可完整檢視該工單者。</summary>
    FileDownload Open(Guid id);
    /// <summary>主管以上或上傳者本人。回傳所屬對象。</summary>
    DeletedAttachment Delete(Guid id);
}
