using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Stn.HackerNews.Api.Infrastructure.Caching;

/// <summary>
/// Two-level cache for multi-instance deployments:
/// L1 = short-lived in-process memory (avoids a Redis round-trip on every request),
/// L2 = Redis (shared by all instances).
/// If Redis is unreachable, the last value this instance saw is served instead of failing.
/// </summary>
public sealed class RedisCacheStore(
    IDistributedCache redis,
    IMemoryCache local,
    IOptions<CachingOptions> options,
    ILogger<RedisCacheStore> logger) : ICacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Fallback when Redis is down and the L1 entry has expired.
    private readonly ConcurrentDictionary<string, object> _lastKnown = new();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        if (local.TryGetValue(key, out T? cached))
        {
            return cached;
        }

        try
        {
            var json = await redis.GetStringAsync(key, ct);
            if (json is null)
            {
                return null;
            }

            var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
            if (value is not null)
            {
                Remember(key, value);
            }

            return value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis read failed for {Key}; falling back to last known local value", key);
            return _lastKnown.TryGetValue(key, out var fallback) ? fallback as T : null;
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct) where T : class
    {
        Remember(key, value);

        try
        {
            await redis.SetStringAsync(key, JsonSerializer.Serialize(value, JsonOptions), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // This instance still serves the value from memory; other instances will refresh themselves.
            logger.LogWarning(ex, "Redis write failed for {Key}", key);
        }
    }

    private void Remember<T>(string key, T value) where T : class
    {
        local.Set(key, value, options.Value.LocalCacheDuration);
        _lastKnown[key] = value;
    }
}