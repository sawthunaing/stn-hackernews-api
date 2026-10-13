using Microsoft.Extensions.Caching.Memory;

namespace Stn.HackerNews.Api.Infrastructure.Caching;

public sealed class MemoryCacheStore(IMemoryCache cache) : ICacheStore
{
    private static readonly MemoryCacheEntryOptions EntryOptions = new()
    {
        Priority = CacheItemPriority.NeverRemove
    };

    public Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class =>
        Task.FromResult(cache.TryGetValue(key, out T? value) ? value : null);

    public Task SetAsync<T>(string key, T value, CancellationToken ct) where T : class
    {
        cache.Set(key, value, EntryOptions);
        return Task.CompletedTask;
    }
}