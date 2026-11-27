using System.Text.Json;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace AssetCare.Infrastructure.Json;

public class JsonDataOptions
{
    /// <summary>相對於 ContentRoot。</summary>
    public string DataDirectory { get; set; } = "App_Data";
    public string SeedDirectory { get; set; } = "SeedData";
}

internal interface IReloadableRepository
{
    void Reload();
}

/// <summary>管理資料目錄、種子資料複製與跨 Repository 的重置鎖。</summary>
public class JsonDataStore : IDataStore
{
    public const string UploadsFolder = "uploads";
    private readonly string _seedDirectory;
    private readonly List<IReloadableRepository> _repositories = [];
    private readonly ILogger<JsonDataStore> _logger;

    /// <summary>一般讀寫取讀取鎖；重置取寫入鎖以獨占整個資料目錄。</summary>
    internal ReaderWriterLockSlim Gate { get; } = new();

    public string DataDirectory { get; }
    public string UploadDirectory => Path.Combine(DataDirectory, UploadsFolder);

    public JsonDataStore(IOptions<JsonDataOptions> options, IHostEnvironment environment, ILogger<JsonDataStore> logger)
    {
        _logger = logger;
        DataDirectory = Path.GetFullPath(options.Value.DataDirectory, environment.ContentRootPath);
        _seedDirectory = Path.GetFullPath(options.Value.SeedDirectory, environment.ContentRootPath);
        EnsureInitialized();
    }

    internal void Register(IReloadableRepository repository)
    {
        lock (_repositories) _repositories.Add(repository);
    }

    public void ResetToSeed()
    {
        // 先確認種子資料完整可讀，避免清掉現有資料後才發現無法還原。
        VerifySeed();

        Gate.EnterWriteLock();
        try
        {
            ClearDirectory(DataDirectory);
            EnsureInitialized();
            lock (_repositories)
                foreach (var repository in _repositories) repository.Reload();
            _logger.LogWarning("資料已重置為範例資料");
        }
        finally
        {
            Gate.ExitWriteLock();
        }
    }

    /// <summary>首次啟動時由 SeedData 複製；之後只補缺少的資料檔，不覆蓋既有資料。</summary>
    private void EnsureInitialized()
    {
        Directory.CreateDirectory(DataDirectory);
        var firstRun = !Directory.EnumerateFiles(DataDirectory, "*.json").Any();

        foreach (var type in JsonFileNames.EntityTypes)
        {
            var fileName = JsonFileNames.For(type);
            var source = Path.Combine(_seedDirectory, fileName);
            var target = Path.Combine(DataDirectory, fileName);
            if (!File.Exists(target) && File.Exists(source)) File.Copy(source, target);
        }

        var seedUploads = Path.Combine(_seedDirectory, UploadsFolder);
        Directory.CreateDirectory(UploadDirectory);
        if (firstRun && Directory.Exists(seedUploads))
            foreach (var file in Directory.EnumerateFiles(seedUploads))
                File.Copy(file, Path.Combine(UploadDirectory, Path.GetFileName(file)), overwrite: true);
    }

    private void VerifySeed()
    {
        foreach (var type in JsonFileNames.EntityTypes)
        {
            var fileName = JsonFileNames.For(type);
            var path = Path.Combine(_seedDirectory, fileName);
            if (!File.Exists(path))
                throw new BusinessRuleException($"缺少範例資料檔 {fileName}，已取消重置。");
            try
            {
                using var stream = File.OpenRead(path);
                JsonSerializer.Deserialize(stream, typeof(List<>).MakeGenericType(type), JsonDefaults.Options);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "範例資料檔格式錯誤 {Path}", path);
                throw new BusinessRuleException($"範例資料檔 {fileName} 格式錯誤，已取消重置。");
            }
        }
    }

    private static void ClearDirectory(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
        foreach (var child in Directory.EnumerateDirectories(directory)) Directory.Delete(child, recursive: true);
    }
}
