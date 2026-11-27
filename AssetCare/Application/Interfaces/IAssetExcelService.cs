using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>資產 Excel 匯入匯出（僅限主管以上）。</summary>
public interface IAssetExcelService
{
    byte[] Export(AssetQuery query);
    byte[] CreateImportTemplate();
    ImportResult Import(UploadedFile file);
}
