using System.Globalization;
using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Exceptions;
using ClosedXML.Excel;

namespace AssetCare.Infrastructure.Excel;

public class ClosedXmlSpreadsheetService(ILogger<ClosedXmlSpreadsheetService> logger) : ISpreadsheetService
{
    private const int MaxColumns = 100;

    public byte[] Write(params SheetData[] sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var data in sheets)
        {
            var sheet = workbook.Worksheets.Add(data.Name);
            for (var c = 0; c < data.Headers.Count; c++)
                sheet.Cell(1, c + 1).Value = data.Headers[c];
            for (var r = 0; r < data.Rows.Count; r++)
                for (var c = 0; c < data.Rows[r].Count; c++)
                    SetValue(sheet.Cell(r + 2, c + 1), data.Rows[r][c]);

            var header = sheet.Range(1, 1, 1, data.Headers.Count);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAD3");
            sheet.SheetView.FreezeRows(1);
            // 依字數估算欄寬，避免依賴系統字型量測。
            for (var c = 0; c < data.Headers.Count; c++)
            {
                var longest = data.Rows.Select(row => Width(row[c])).Append(Width(data.Headers[c])).Max();
                sheet.Column(c + 1).Width = Math.Clamp(longest + 2, 10, 50);
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public SheetContent Read(Stream content, int maxRows)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.First();
            var lastColumn = Math.Min(sheet.LastColumnUsed()?.ColumnNumber() ?? 0, MaxColumns);
            var headers = Enumerable.Range(1, lastColumn).Select(c => Text(sheet.Cell(1, c))).ToList();

            var rows = new List<SheetRow>();
            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > 1))
            {
                var values = new Dictionary<string, string>();
                for (var c = 1; c <= lastColumn; c++)
                    if (headers[c - 1].Length > 0) values[headers[c - 1]] = Text(sheet.Cell(row.RowNumber(), c));
                if (values.Values.All(v => v.Length == 0)) continue;
                if (rows.Count == maxRows)
                    throw new BusinessRuleException($"一次最多匯入 {maxRows} 筆資料。");
                rows.Add(new SheetRow(row.RowNumber(), values));
            }
            return new SheetContent(headers, rows);
        }
        catch (Exception ex) when (ex is not BusinessRuleException)
        {
            // 毀損或非 Excel 的檔案可能丟出各種例外。
            logger.LogWarning(ex, "無法讀取上傳的 Excel 檔案");
            throw new BusinessRuleException("無法讀取檔案，請確認是有效的 .xlsx 檔案。");
        }
    }

    private static string Text(IXLCell cell)
    {
        var value = cell.Value;
        return value.Type switch
        {
            XLDataType.Blank => "",
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.Number => ((decimal)value.GetNumber()).ToString(CultureInfo.InvariantCulture),
            _ => value.ToString(CultureInfo.InvariantCulture).Trim()
        };
    }

    // 字串一律以文字值寫入，不會被當成公式執行。
    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: break;
            case string s: cell.Value = s; break;
            case int i: cell.Value = i; break;
            case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = "#,##0.##"; break;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "yyyy-mm-dd";
                break;
            case DateTime dateTime:
                cell.Value = dateTime;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                break;
            default: cell.Value = value.ToString(); break;
        }
    }

    /// <summary>中日韓字元約佔兩個字寬。</summary>
    private static int Width(object? value) => value switch
    {
        null => 0,
        DateOnly => 10,
        DateTime => 16,
        _ => (value.ToString() ?? "").Sum(c => c > 0x2E7F ? 2 : 1)
    };
}
