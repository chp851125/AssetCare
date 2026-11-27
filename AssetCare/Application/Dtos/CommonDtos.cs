namespace AssetCare.Application.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageCount(Total, PageSize);

    /// <summary>由完整清單切出指定頁；頁碼超出範圍時自動修正。</summary>
    public static PagedResult<T> From(IReadOnlyList<T> all, int page, int pageSize)
    {
        page = Math.Clamp(page, 1, PageCount(all.Count, pageSize));
        return new PagedResult<T>(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count);
    }

    private static int PageCount(int total, int pageSize) => Math.Max(1, (total + pageSize - 1) / pageSize);
}

/// <summary>下拉選單項目。</summary>
public record Option(Guid Id, string Text);

public record UploadedFile(string FileName, long Length, Stream Content);

public record FileDownload(Stream Content, string ContentType, string FileName);

public record SheetData(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows);

public record SheetRow(int RowNumber, IReadOnlyDictionary<string, string> Values)
{
    public string Get(string header) => Values.GetValueOrDefault(header, "");
}

public record SheetContent(IReadOnlyList<string> Headers, IReadOnlyList<SheetRow> Rows);
