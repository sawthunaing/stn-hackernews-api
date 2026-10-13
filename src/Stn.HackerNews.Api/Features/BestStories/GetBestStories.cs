using Stn.HackerNews.Api.Infrastructure.Caching;

namespace Stn.HackerNews.Api.Features.BestStories;

public sealed record GetBestStoriesQuery(int Count);

public sealed class GetBestStoriesHandler(ICacheStore cache, BestStoriesRefresher refresher)
{
    public async Task<IReadOnlyList<StoryDto>> HandleAsync(GetBestStoriesQuery query, CancellationToken ct)
    {
        var snapshot = await cache.GetAsync<BestStoriesSnapshot>(BestStoriesRefresher.CacheKey, ct)
                       ?? await refresher.GetOrLoadAsync(ct);

        return snapshot.Stories.Take(query.Count).ToArray();
    }
}

public static class GetBestStoriesEndpoint
{
    public static IEndpointRouteBuilder MapGetBestStories(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stories/best", HandleAsync)
            .WithName("GetBestStories")
            .WithTags("Stories")
            .WithSummary("Returns the best n Hacker News stories, sorted by score (descending).")
            .Produces<IReadOnlyList<StoryDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> HandleAsync(int? n, GetBestStoriesHandler handler, CancellationToken ct)
    {
        if (n is null or < 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["n"] = ["Query parameter 'n' is required and must be a positive integer."]
            });
        }

        try
        {
            return Results.Ok(await handler.HandleAsync(new GetBestStoriesQuery(n.Value), ct));
        }
        catch (StoriesUnavailableException)
        {
            return Results.Problem(
                title: "Stories are temporarily unavailable",
                detail: "The Hacker News API could not be reached. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}