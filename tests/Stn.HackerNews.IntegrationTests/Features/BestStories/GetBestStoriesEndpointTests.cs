using System.Net;
using System.Text.Json;
using Stn.HackerNews.Tests.Shared;

namespace Stn.HackerNews.IntegrationTests.Features.BestStories;

public class GetBestStoriesEndpointTests
{
    [Fact]
    public async Task Returns_stories_in_the_required_json_shape_sorted_by_score()
    {
        using var factory = new ApiFactory();
        factory.HackerNews.Items[21233041] = FakeHackerNewsClient.Story(21233041, 1716);
        factory.HackerNews.Items[2] = FakeHackerNewsClient.Story(2, 5);
        factory.HackerNews.Items[3] = FakeHackerNewsClient.Story(3, 900);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stories/best?n=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var stories = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, stories.Length);

        var first = stories[0];
        Assert.Equal(
            new[] { "title", "uri", "postedBy", "time", "score", "commentCount" },
            first.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Story 21233041", first.GetProperty("title").GetString());
        Assert.Equal("https://example.com/21233041", first.GetProperty("uri").GetString());
        Assert.Equal("user21233041", first.GetProperty("postedBy").GetString());
        Assert.Equal("2019-10-12T13:43:01+00:00", first.GetProperty("time").GetString());
        Assert.Equal(1716, first.GetProperty("score").GetInt32());
        Assert.Equal(572, first.GetProperty("commentCount").GetInt32());

        Assert.Equal(900, stories[1].GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task Returns_all_stories_when_n_exceeds_available()
    {
        using var factory = new ApiFactory();
        factory.HackerNews.Items[1] = FakeHackerNewsClient.Story(1, 10);
        factory.HackerNews.Items[2] = FakeHackerNewsClient.Story(2, 20);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stories/best?n=500");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, json.RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData("/api/stories/best")]
    [InlineData("/api/stories/best?n=0")]
    [InlineData("/api/stories/best?n=-3")]
    public async Task Invalid_n_returns_400_problem_details(string url)
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("n", out _));
    }

    [Fact]
    public async Task Returns_503_problem_details_when_hacker_news_is_unavailable()
    {
        using var factory = new ApiFactory();
        factory.HackerNews.FailBestStories = true;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stories/best?n=5");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Many_concurrent_requests_result_in_a_single_hacker_news_load()
    {
        using var factory = new ApiFactory();
        factory.HackerNews.BestStoriesDelay = TimeSpan.FromMilliseconds(200);
        for (var i = 1; i <= 50; i++) factory.HackerNews.Items[i] = FakeHackerNewsClient.Story(i, i);
        var client = factory.CreateClient();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 200).Select(_ => client.GetAsync("/api/stories/best?n=10")));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, factory.HackerNews.BestStoryIdCalls);
    }
}