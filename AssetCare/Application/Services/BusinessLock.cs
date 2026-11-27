using AssetCare.Application.Interfaces;

namespace AssetCare.Application.Services;

/// <summary>單一行程內的鎖，需註冊為 Singleton。可重入。</summary>
public class BusinessLock : IBusinessLock
{
    private readonly Lock _lock = new();

    public T Run<T>(Func<T> action)
    {
        lock (_lock) return action();
    }

    public void Run(Action action)
    {
        lock (_lock) action();
    }
}
