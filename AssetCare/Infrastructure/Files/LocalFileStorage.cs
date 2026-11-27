using AssetCare.Application.Interfaces;
using AssetCare.Infrastructure.Json;

namespace AssetCare.Infrastructure.Files;

/// <summary>附件存於資料目錄的 uploads 子目錄。</summary>
public class LocalFileStorage(JsonDataStore store) : IFileStorage
{
    public string Save(Stream content, string extension)
    {
        var name = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        Directory.CreateDirectory(store.UploadDirectory);
        using var target = new FileStream(PathOf(name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        content.CopyTo(target);
        return name;
    }

    public Stream? OpenRead(string storedFileName)
    {
        var path = PathOf(storedFileName);
        return File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)
            : null;
    }

    public void Delete(string storedFileName) => File.Delete(PathOf(storedFileName));

    // 只取檔名部分，避免資料檔被改成路徑跳脫字串。
    private string PathOf(string storedFileName) =>
        Path.Combine(store.UploadDirectory, Path.GetFileName(storedFileName));
}
