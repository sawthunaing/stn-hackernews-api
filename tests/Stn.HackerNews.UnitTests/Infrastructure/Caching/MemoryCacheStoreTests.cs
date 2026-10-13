using Microsoft.Extensions.Caching.Memory;
using Stn.HackerNews.Api.Infrastructure.Caching;

namespace Stn.HackerNews.UnitTests.Infrastructure.Caching;

public class MemoryCacheStoreTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static MemoryCacheStore CreateStore() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task GetAsync_returns_null_when_key_is_missing()
    {
        var store = CreateStore();

        var result = await store.GetAsync<string>("missing", Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_returns_value_that_was_set()
    {
        var store = CreateStore();
        var value = new List<int> { 1, 2, 3 };

        await store.SetAsync("key", value, Ct);
        var result = await store.GetAsync<List<int>>("key", Ct);

        Assert.Same(value, result);
    }

    [Fact]
    public async Task SetAsync_replaces_existing_value()
    {
        var store = CreateStore();

        await store.SetAsync("key", "old", Ct);
        await store.SetAsync("key", "new", Ct);

        Assert.Equal("new", await store.GetAsync<string>("key", Ct));
    }
}