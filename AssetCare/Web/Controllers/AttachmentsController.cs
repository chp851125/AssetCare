using AssetCare.Application.Interfaces;
using AssetCare.Domain.Enums;
using AssetCare.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

/// <summary>下載與刪除依附件所屬對象授權（見 AttachmentService）；資產附件上傳限員工。</summary>
public class AttachmentsController(IAttachmentService attachments) : Controller
{
    [HttpGet]
    public IActionResult Download(Guid id)
    {
        var file = attachments.Open(id);
        Response.Headers.XContentTypeOptions = "nosniff";
        // 只有圖片直接顯示，其他類型一律下載。
        return file.ContentType.StartsWith("image/", StringComparison.Ordinal)
            ? File(file.Content, file.ContentType)
            : File(file.Content, file.ContentType, file.FileName);
    }

    [Authorize(Roles = Roles.Staff), HttpPost, RequestSizeLimit(55 * 1024 * 1024)]
    public IActionResult UploadForAsset(Guid assetId, List<IFormFile> files, string? caption)
    {
        attachments.UploadForAsset(assetId, files.Select(f => f.ToUpload(HttpContext)).ToList(), caption);
        TempData[Flash.Success] = "附件已上傳。";
        return RedirectToAction("Details", "Assets", new { id = assetId }, "attachments");
    }

    [HttpPost]
    public IActionResult Delete(Guid id)
    {
        var deleted = attachments.Delete(id);
        TempData[Flash.Success] = "附件已刪除。";
        return deleted.OwnerType == AttachmentOwnerType.Asset
            ? RedirectToAction("Details", "Assets", new { id = deleted.OwnerId }, "attachments")
            : RedirectToAction("Details", "WorkOrders", new { id = deleted.OwnerId }, "photos");
    }
}
