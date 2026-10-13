using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Stn.HackerNews.Api.Features.BestStories;
using Stn.HackerNews.Api.Infrastructure.Caching;
using Stn.HackerNews.Api.Infrastructure.HackerNews;
using Stn.HackerNews.Tests.Shared;

namespace Stn.HackerNews.UnitTests.Features.BestStories;

public class GetBestStoriesHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeHackerNewsClient _client = new();
    private readonly GetBestStoriesHandler _handler;

    public GetBestStoriesHandlerTests()
    {
        var cache = new MemoryCacheStore(new MemoryCache(new MemoryCacheOptions()));
        var refresher = new BestStoriesRefresher(
            _client,
            cache,
            Options.Create(new BestStoriesOptions()),
            Options.Create(new HackerNewsOptions()),
            new FakeTimeProvider(),
            NullLogger<BestStoriesRefresher>.Instance);

        _handler = new GetBestStoriesHandler(cache, refresher);
    }

    private Task<IReadOnlyList<StoryDto>> GetAsync(int count) =>
        _handler.HandleAsync(new GetBestStoriesQuery(count), Ct);

    [Fact]
    public async Task Returns_top_n_stories_in_descending_score_order()
    {
        _client.Items[1] = FakeHackerNewsClient.Story(1, 50);
        _client.Items[2] = FakeHackerNewsClient.Story(2, 300);
        _client.Items[3] = FakeHackerNewsClient.Story(3, 120);

        var result = await GetAsync(2);

        Assert.Equal(new[] { 300, 120 }, result.Select(s => s.Score));
    }

    [Fact]
    public async Task Returns_all_stories_when_n_exceeds_available()
    {
        _client.Items[1] = FakeHackerNewsClient.Story(1, 10);
        _client.Items[2] = FakeHackerNewsClient.Story(2, 20);

        var result = await GetAsync(500);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Warm_cache_serves_requests_without_calling_hacker_news()
    {
        _client.Items[1] = FakeHackerNewsClient.Story(1, 10);
        await GetAsync(1);

        for (var i = 0; i < 100; i++)
        {
            await GetAsync(1);
        }

        Assert.Equal(1, _client.BestStoryIdCalls);
    }

    [Fact]
    public async Task Throws_stories_unavailable_when_hacker_news_fails_on_cold_cache()
    {
        _client.FailBestStories = true;

        await Assert.ThrowsAsync<StoriesUnavailableException>(() => GetAsync(5));
    }
}