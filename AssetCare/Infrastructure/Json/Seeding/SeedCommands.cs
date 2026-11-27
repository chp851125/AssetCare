using System.Text.Json;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AssetCare.Infrastructure.Json.Seeding;

/// <summary>命令列工具：--generate-seed（僅限 Development）與 --validate-seed。</summary>
public static class SeedCommands
{
    public const string Generate = "--generate-seed";
    public const string Validate = "--validate-seed";

    public static bool IsSeedCommand(string[] args) => args.Contains(Generate) || args.Contains(Validate);

    /// <returns>程序結束代碼：0 成功。</returns>
    public static int Run(string[] args, IServiceProvider services, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            Console.Error.WriteLine("範例資料工具僅能在 Development 環境執行。");
            return 1;
        }

        var time = services.GetRequiredService<ITimeProvider>();
        var options = services.GetRequiredService<IOptions<JsonDataOptions>>().Value;
        var seedDirectory = Path.GetFullPath(options.SeedDirectory, environment.ContentRootPath);
        var generate = args.Contains(Generate);

        SeedDataSet data;
        if (generate)
        {
            var catalogPath = Path.Combine(seedDirectory, "_source", "catalog.json");
            var catalog = JsonSerializer.Deserialize<SeedCatalog>(File.ReadAllText(catalogPath), JsonDefaults.Options)
                          ?? throw new InvalidOperationException($"無法讀取 {catalogPath}");
            data = new SeedGenerator(catalog, time, services.GetRequiredService<IPasswordHasher<User>>()).Generate();
        }
        else
        {
            data = SeedDataSet.Load(seedDirectory);
        }

        var errors = new SeedValidator(time).Validate(data);
        if (errors.Count > 0)
        {
            Console.Error.WriteLine($"驗證失敗，共 {errors.Count} 項{(generate ? "，未寫入任何檔案" : "")}：");
            foreach (var error in errors) Console.Error.WriteLine("  - " + error);
            return 1;
        }

        if (generate) data.Save(seedDirectory);

        Console.WriteLine(generate ? $"已產生並通過驗證：{seedDirectory}" : "驗證通過。");
        foreach (var group in data.All.GroupBy(e => e.GetType().Name).OrderBy(g => g.Key))
            Console.WriteLine($"  {group.Key,-18}{group.Count(),4}");
        Console.WriteLine($"  {"uploads",-18}{data.Uploads.Count,4}");
        if (generate) Console.WriteLine("如需套用到執行中的資料，請使用「重置為範例資料」或刪除 App_Data 資料夾。");
        return 0;
    }
}
