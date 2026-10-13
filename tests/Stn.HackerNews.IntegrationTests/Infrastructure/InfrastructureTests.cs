using System.Net;
using Stn.HackerNews.Tests.Shared;

namespace Stn.HackerNews.IntegrationTests.Infrastructure;

public class InfrastructureTests
{
    [Fact]
    public async Task Requests_over_the_rate_limit_return_429_with_retry_after()
    {
        using var factory = new ApiFactory { RateLimitPermits = 2 };
        factory.HackerNews.Items[1] = FakeHackerNewsClient.Story(1, 10);
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
        {
            last = await client.GetAsync("/api/stories/best?n=1");
            statuses.Add(last.StatusCode);
        }

        Assert.Equal(
            new[] { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests },
            statuses);
        Assert.NotNull(last!.Headers.RetryAfter);
        Assert.Equal("application/problem+json", last.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Health_endpoint_is_not_rate_limited()
    {
        using var factory = new ApiFactory { RateLimitPermits = 1 };
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task OpenApi_document_describes_the_best_stories_endpoint()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/stories/best", await response.Content.ReadAsStringAsync());
    }
}