using AssetCare.Infrastructure.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssetCare.Web.Infrastructure;

/// <summary>確認資料目錄可寫入。</summary>
public class DataDirectoryHealthCheck(JsonDataStore store) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var probe = Path.Combine(store.DataDirectory, $".health-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("資料目錄無法寫入", ex));
        }
    }
}
