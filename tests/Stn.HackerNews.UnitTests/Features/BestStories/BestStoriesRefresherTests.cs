using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Stn.HackerNews.Api.Features.BestStories;
using Stn.HackerNews.Api.Infrastructure.Caching;
using Stn.HackerNews.Api.Infrastructure.HackerNews;
using Stn.HackerNews.Tests.Shared;

namespace Stn.HackerNews.UnitTests.Features.BestStories;

public class BestStoriesRefresherTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeHackerNewsClient _client = new();
    private readonly FakeTimeProvider _time = new();
    private readonly BestStoriesRefresher _refresher;

    public BestStoriesRefresherTests()
    {
        _refresher = new BestStoriesRefresher(
            _client,
            new MemoryCacheStore(new MemoryCache(new MemoryCacheOptions())),
            Options.Create(new BestStoriesOptions { FailureCooldown = TimeSpan.FromSeconds(10) }),
            Options.Create(new HackerNewsOptions { MaxConcurrentRequests = 10 }),
            _time,
            NullLogger<BestStoriesRefresher>.Instance);
    }

    [Fact]
    public async Task Snapshot_is_sorted_by_score_descending()
    {
        _client.Items[1] = FakeHackerNewsClient.Story(1, 50);
        _client.Items[2] = FakeHackerNewsClient.Story(2, 300);
        _client.Items[3] = FakeHackerNewsClient.Story(3, 120);

        var snapshot = await _refresher.GetOrLoadAsync(Ct);

        Assert.Equal(new[] { 300, 120, 50 }, snapshot.Stories.Select(s => s.Score));
    }

    [Fact]
    public async Task Deleted_dead_and_missing_items_are_skipped()
    {
        _client.Items[1] = FakeHackerNewsClient.Story(1, 10);
        _client.Items[2] = new HackerNewsItem { Id = 2, Deleted = true, Score = 999 };
        _client.Items[3] = new HackerNewsItem { Id = 3, Dead = true, Score = 999 };
        _client.Items[4] = null;

        var snapshot = await _refresher.GetOrLoadAsync(Ct);

        Assert.Single(snapshot.Stories);
    }

    [Fact]
    public async Task Concurrent_requests_on_cold_cache_call_hacker_news_once()
    {
        _client.BestStoriesDelay = TimeSpan.FromMilliseconds(100);
        for (var i = 1; i <= 50; i++) _client.Items[i] = FakeHackerNewsClient.Story(i, i);

        var snapshots = await Task.WhenAll(
            Enumerable.Range(0, 1000).Select(_ => _refresher.GetOrLoadAsync(Ct)));

        Assert.Equal(1, _client.BestStoryIdCalls);
        Assert.All(snapshots, s => Assert.Equal(50, s.Stories.Count));
    }

    [Fact]
    public async Task Item_requests_never_exceed_max_concurrency()
    {
        _client.ItemDelay = TimeSpan.FromMilliseconds(20);
        for (var i = 1; i <= 50; i++) _client.Items[i] = FakeHackerNewsClient.Story(i, i);

        await _refresher.GetOrLoadAsync(Ct);

        Assert.InRange(_client.MaxConcurrentItemRequests, 1, 10);
    }

    [Fact]
    public async Task Fresh_snapshot_is_reused_and_stale_snapshot_is_refreshed()
    {
        _client.Items[1] = FakeHackerNewsClient.Story(1, 10);
        var maxAge = TimeSpan.FromMinutes(2.5);
        await _refresher.GetOrLoadAsync(Ct);

        _time.Advance(TimeSpan.FromMinutes(1));
        await _refresher.RefreshIfOlderThanAsync(maxAge, Ct);
        Assert.Equal(1, _client.BestStoryIdCalls);

        _time.Advance(TimeSpan.FromMinutes(2));
        await _refresher.RefreshIfOlderThanAsync(maxAge, Ct);
        Assert.Equal(2, _client.BestStoryIdCalls);
    }

    [Fact]
    public async Task Failed_item_keeps_its_previous_version()
    {
        _client.Items[1] = FakeHackerNewsClient.Story(1, 10);
        _client.Items[2] = FakeHackerNewsClient.Story(2, 20);
        await _refresher.GetOrLoadAsync(Ct);

        _client.FailingIds.Add(2);
        var snapshot = await _refresher.RefreshIfOlderThanAsync(TimeSpan.Zero, Ct);

        Assert.Contains(snapshot.Stories, s => s.Score == 20);
    }

    [Fact]
    public async Task Failed_refresh_keeps_serving_previous_snapshot()
    {
        _client.Items[1] = FakeHackerNewsClient.Story(1, 10);
        await _refresher.GetOrLoadAsync(Ct);

        _client.FailBestStories = true;
        await Assert.ThrowsAsync<StoriesUnavailableException>(
            () => _refresher.RefreshIfOlderThanAsync(TimeSpan.Zero, Ct));

        var snapshot = await _refresher.GetOrLoadAsync(Ct);
        Assert.Single(snapshot.Stories);
    }

    [Fact]
    public async Task Cold_cache_failure_triggers_cooldown_then_retries_after_it()
    {
        _client.FailBestStories = true;

        for (var i = 0; i < 20; i++)
        {
            await Assert.ThrowsAsync<StoriesUnavailableException>(() => _refresher.GetOrLoadAsync(Ct));
        }
        Assert.Equal(1, _client.BestStoryIdCalls);

        _time.Advance(TimeSpan.FromSeconds(11));
        _client.FailBestStories = false;
        _client.Items[1] = FakeHackerNewsClient.Story(1, 10);

        var snapshot = await _refresher.GetOrLoadAsync(Ct);
        Assert.Single(snapshot.Stories);
        Assert.Equal(2, _client.BestStoryIdCalls);
    }
}