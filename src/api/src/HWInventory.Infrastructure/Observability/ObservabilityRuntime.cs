using System;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace HWInventory.Infrastructure.Observability;

public class ObservabilityRuntime : IObservabilityRuntime
{
    private static readonly string CacheKey = "observability:configuration";

    private readonly IObservabilityConfigurationStore _store;
    private readonly IMemoryCache _cache;

    public ObservabilityRuntime(IObservabilityConfigurationStore store, IMemoryCache cache)
    {
        _store = store;
        _cache = cache;
    }

    public async Task<ObservabilityConfigurationModel> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out ObservabilityConfigurationModel? cached) && cached is not null)
        {
            return cached;
        }

        var configuration = await _store.GetAsync(cancellationToken);
        _cache.Set(CacheKey, configuration, TimeSpan.FromMinutes(1));
        return configuration;
    }

    public void Invalidate()
    {
        _cache.Remove(CacheKey);
    }
}
