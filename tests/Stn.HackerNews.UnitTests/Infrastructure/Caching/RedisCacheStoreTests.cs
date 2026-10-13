using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stn.HackerNews.Api.Features.BestStories;
using Stn.HackerNews.Api.Infrastructure.Caching;

namespace Stn.HackerNews.UnitTests.Infrastructure.Caching;

public class RedisCacheStoreTests
{
    private const string Key = "snapshot";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static readonly BestStoriesSnapshot Snapshot = new(
        [
            new StoryDto("First", "https://example.com/1", "alice",
                new DateTimeOffset(2019, 10, 12, 13, 43, 1, TimeSpan.Zero), 300, 10),
            new StoryDto("Second", "https://example.com/2", "bob",
                new DateTimeOffset(2019, 10, 11, 8, 0, 0, TimeSpan.Zero), 100, 5)
        ],
        new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));

    private static RedisCacheStore CreateStore(IDistributedCache redis, TimeSpan? localDuration = null) =>
        new(
            redis,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CachingOptions { LocalCacheDuration = localDuration ?? TimeSpan.FromSeconds(30) }),
            NullLogger<RedisCacheStore>.Instance);

    private static IDistributedCache CreateSharedRedis() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    [Fact]
    public async Task Value_written_by_one_instance_is_read_by_another()
    {
        var redis = CreateSharedRedis();
        var instanceA = CreateStore(redis);
        var instanceB = CreateStore(redis);

        await instanceA.SetAsync(Key, Snapshot, Ct);
        var result = await instanceB.GetAsync<BestStoriesSnapshot>(Key, Ct);

        Assert.NotNull(result);
        Assert.NotSame(Snapshot, result);                // came through Redis as a copy
        Assert.Equal(Snapshot.Stories, result.Stories);  // but with identical content
        Assert.Equal(Snapshot.RefreshedAt, result.RefreshedAt);
    }

    [Fact]
    public async Task Repeated_reads_on_same_instance_are_served_from_local_memory()
    {
        var store = CreateStore(CreateSharedRedis());

        await store.SetAsync(Key, Snapshot, Ct);
        var result = await store.GetAsync<BestStoriesSnapshot>(Key, Ct);

        Assert.Same(Snapshot, result); // L1 hit: no Redis round-trip, no deserialization
    }

    [Fact]
    public async Task Redis_failures_do_not_throw()
    {
        var store = CreateStore(new FailingDistributedCache());

        await store.SetAsync(Key, Snapshot, Ct);
        var result = await store.GetAsync<BestStoriesSnapshot>(Key, Ct);

        Assert.Same(Snapshot, result);
    }

    [Fact]
    public async Task Redis_down_after_local_expiry_serves_last_known_value()
    {
        var store = CreateStore(new FailingDistributedCache(), localDuration: TimeSpan.FromMilliseconds(1));

        await store.SetAsync(Key, Snapshot, Ct);
        await Task.Delay(50); // let the L1 entry expire

        var result = await store.GetAsync<BestStoriesSnapshot>(Key, Ct);

        Assert.Same(Snapshot, result);
    }

    /// <summary>Simulates Redis being unreachable.</summary>
    private sealed class FailingDistributedCache : IDistributedCache
    {
        private static Exception Down() => new InvalidOperationException("Redis is down");

        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
    }
}