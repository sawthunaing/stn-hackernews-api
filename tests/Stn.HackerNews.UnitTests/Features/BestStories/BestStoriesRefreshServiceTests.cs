using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Stn.HackerNews.Api.Features.BestStories;
using Stn.HackerNews.Api.Infrastructure.Caching;
using Stn.HackerNews.Api.Infrastructure.HackerNews;
using Stn.HackerNews.Tests.Shared;

namespace Stn.HackerNews.UnitTests.Features.BestStories;

public class BestStoriesRefreshServiceTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly FakeHackerNewsClient _client = new();
    private readonly FakeTimeProvider _time = new();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly BestStoriesRefreshService _service;

    public BestStoriesRefreshServiceTests()
    {
        _client.Items[1] = FakeHackerNewsClient.Story(1, 10);

        var options = Options.Create(new BestStoriesOptions
        {
            RefreshInterval = Interval,
            FailureCooldown = TimeSpan.FromSeconds(10)
        });

        var refresher = new BestStoriesRefresher(
            _client,
            new MemoryCacheStore(_memoryCache),
            options,
            Options.Create(new HackerNewsOptions()),
            _time,
            NullLogger<BestStoriesRefresher>.Instance);

        _service = new BestStoriesRefreshService(
            refresher, options, _time, NullLogger<BestStoriesRefreshService>.Instance);
    }

    [Fact]
    public async Task Warms_cache_on_startup()
    {
        await RunServiceAsync(async () =>
        {
            await WaitForRefreshAtCurrentTimeAsync();

            Assert.Equal(1, _client.BestStoryIdCalls);
        });
    }

    [Fact]
    public async Task Refreshes_on_each_interval()
    {
        await RunServiceAsync(async () =>
        {
            await WaitForRefreshAtCurrentTimeAsync(); // warm-up finished

            _time.Advance(Interval);
            await WaitForRefreshAtCurrentTimeAsync(); // first scheduled refresh finished

            _time.Advance(Interval);
            await WaitForRefreshAtCurrentTimeAsync(); // second scheduled refresh finished

            Assert.Equal(3, _client.BestStoryIdCalls);
        });
    }

    [Fact]
    public async Task Keeps_running_after_a_failed_refresh()
    {
        _client.FailBestStories = true;

        await RunServiceAsync(async () =>
        {
            await WaitUntilAsync(() => _client.BestStoryIdCalls >= 1);
            Assert.Null(CachedSnapshot);

            _client.FailBestStories = false;

            // Keep moving the clock forward until a scheduled refresh succeeds.
            // This can only happen if the service survived the failed warm-up.
            await WaitUntilAsync(() =>
            {
                _time.Advance(Interval);
                return CachedSnapshot is not null;
            });
        });
    }

    private BestStoriesSnapshot? CachedSnapshot =>
        _memoryCache.TryGetValue(BestStoriesRefresher.CacheKey, out BestStoriesSnapshot? snapshot)
            ? snapshot
            : null;

    /// <summary>Starts the service, runs the test body, and always stops the service.</summary>
    private async Task RunServiceAsync(Func<Task> testBody)
    {
        await _service.StartAsync(CancellationToken.None);
        try
        {
            await testBody();
        }
        finally
        {
            await _service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Waits until a refresh has finished (not just started) at the current fake time.</summary>
    private Task WaitForRefreshAtCurrentTimeAsync()
    {
        var now = _time.GetUtcNow();
        return WaitUntilAsync(() => CachedSnapshot?.RefreshedAt == now);
    }

    /// <summary>Polls until the condition is true, failing after 5 real seconds.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }
}