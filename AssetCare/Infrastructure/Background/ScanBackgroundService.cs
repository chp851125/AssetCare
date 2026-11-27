using AssetCare.Application.Interfaces;

namespace AssetCare.Infrastructure.Background;

/// <summary>啟動時執行一次，之後每 5 分鐘掃描 SLA 逾時與到期提醒。</summary>
public class ScanBackgroundService(IServiceScopeFactory scopes, ILogger<ScanBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 讓出啟動流程，掃描不延後網站開始服務。
        await Task.Yield();
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                Run("SLA 逾時", services =>
                {
                    var count = services.GetRequiredService<ISlaService>().ScanOverdue();
                    if (count > 0) logger.LogInformation("新增 {Count} 張逾時工單", count);
                });
                Run("到期提醒", services => services.GetRequiredService<IReminderService>().SendDueReminders());
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { }
    }

    private void Run(string name, Action<IServiceProvider> action)
    {
        try
        {
            using var scope = scopes.CreateScope();
            action(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            // 單次失敗不可讓背景服務停止。
            logger.LogError(ex, "背景掃描失敗：{Name}", name);
        }
    }
}
