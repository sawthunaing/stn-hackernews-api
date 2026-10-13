namespace Stn.HackerNews.Api.Infrastructure.Caching;

public enum CacheProvider
{
    /// <summary>In-process IMemoryCache. Best for a single instance; no external dependencies.</summary>
    Memory,

    /// <summary>L1 in-process memory + L2 Redis. Lets multiple instances share one snapshot.</summary>
    Redis
}

public sealed class CachingOptions
{
    public const string SectionName = "Caching";

    public CacheProvider Provider { get; set; } = CacheProvider.Memory;

    public string RedisConnectionString { get; set; } = "localhost:6379,abortConnect=false";

    /// <summary>Redis mode only: how long a value read from Redis is kept in local memory (L1).</summary>
    public TimeSpan LocalCacheDuration { get; set; } = TimeSpan.FromSeconds(30);
}