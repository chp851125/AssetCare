using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

public interface ISpreadsheetService
{
    byte[] Write(params SheetData[] sheets);
    /// <summary>讀取第一個工作表；首列為欄名，日期轉為 yyyy-MM-dd，空白列略過。</summary>
    SheetContent Read(Stream content, int maxRows);
}
