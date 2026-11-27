namespace AssetCare.Application.Interfaces;

/// <summary>附件實體檔的儲存位置。</summary>
public interface IFileStorage
{
    /// <summary>以隨機檔名保存，回傳儲存檔名。</summary>
    string Save(Stream content, string extension);
    Stream? OpenRead(string storedFileName);
    void Delete(string storedFileName);
}
