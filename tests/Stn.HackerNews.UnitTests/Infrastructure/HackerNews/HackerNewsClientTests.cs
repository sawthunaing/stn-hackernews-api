using System.Net;
using System.Text;
using Stn.HackerNews.Api.Infrastructure.HackerNews;

namespace Stn.HackerNews.UnitTests.Infrastructure.HackerNews;

public class HackerNewsClientTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly StubHttpMessageHandler _handler = new();
    private readonly HackerNewsClient _client;

    public HackerNewsClientTests()
    {
        _client = new HackerNewsClient(new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
        });
    }

    [Fact]
    public async Task GetBestStoryIdsAsync_parses_id_array_from_beststories_endpoint()
    {
        _handler.Responses["/v0/beststories.json"] = "[21233041, 21232933, 21233229]";

        var ids = await _client.GetBestStoryIdsAsync(Ct);

        Assert.Equal(new[] { 21233041, 21232933, 21233229 }, ids);
    }

    [Fact]
    public async Task GetItemAsync_parses_real_hacker_news_item_json()
    {
        // Shape copied from https://hacker-news.firebaseio.com/v0/item/21233041.json
        _handler.Responses["/v0/item/21233041.json"] = """
            {
              "by": "ismaildonmez",
              "descendants": 572,
              "id": 21233041,
              "kids": [21233229, 21233577],
              "score": 1716,
              "time": 1570887781,
              "title": "A uBlock Origin update was rejected from the Chrome Web Store",
              "type": "story",
              "url": "https://github.com/uBlockOrigin/uBlock-issues/issues/745"
            }
            """;

        var item = await _client.GetItemAsync(21233041, Ct);

        Assert.NotNull(item);
        Assert.Equal(21233041, item.Id);
        Assert.Equal("ismaildonmez", item.By);
        Assert.Equal(572, item.Descendants);
        Assert.Equal(1716, item.Score);
        Assert.Equal(1570887781, item.Time);
        Assert.Equal("A uBlock Origin update was rejected from the Chrome Web Store", item.Title);
        Assert.Equal("https://github.com/uBlockOrigin/uBlock-issues/issues/745", item.Url);
        Assert.False(item.Deleted);
        Assert.False(item.Dead);
    }

    [Fact]
    public async Task GetItemAsync_returns_null_when_hacker_news_returns_null()
    {
        _handler.Responses["/v0/item/999999999.json"] = "null";

        var item = await _client.GetItemAsync(999999999, Ct);

        Assert.Null(item);
    }

    [Fact]
    public async Task GetItemAsync_throws_on_http_error()
    {
        // No response registered → stub returns 500.
        await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetItemAsync(1, Ct));
    }

    /// <summary>Returns canned JSON by request path instead of calling the network.</summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public Dictionary<string, string> Responses { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = Responses.TryGetValue(request.RequestUri!.AbsolutePath, out var json)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.InternalServerError);

            return Task.FromResult(response);
        }
    }
}