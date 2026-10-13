using Stn.HackerNews.Api.Features.BestStories;
using Stn.HackerNews.Api.Infrastructure.Caching;
using Stn.HackerNews.Api.Infrastructure.HackerNews;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHackerNewsClient(builder.Configuration)
    .AddAppCaching(builder.Configuration)
    .AddBestStoriesFeature(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => "Hacker News Best Stories API is running");

// TEMPORARY: manual check that the client works. Removed in Step 6.
app.MapGet("/debug/best-ids", (IHackerNewsClient client, CancellationToken ct) =>
    client.GetBestStoryIdsAsync(ct));

app.MapGet("/debug/item/{id:int}", async (int id, IHackerNewsClient client, CancellationToken ct) =>
    await client.GetItemAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound());

app.MapGet("/debug/story/{id:int}", async (int id, IHackerNewsClient client, CancellationToken ct) =>
    await client.GetItemAsync(id, ct) is { } item ? Results.Ok(StoryDto.FromItem(item)) : Results.NotFound());

app.MapGet("/debug/cached-story/{id:int}",
    async (int id, IHackerNewsClient client, ICacheStore cache, CancellationToken ct) =>
    {
        var key = $"debug:story:{id}";

        if (await cache.GetAsync<StoryDto>(key, ct) is { } cached)
        {
            return Results.Ok(new { source = "cache", story = cached });
        }

        if (await client.GetItemAsync(id, ct) is not { } item)
        {
            return Results.NotFound();
        }

        var story = StoryDto.FromItem(item);
        await cache.SetAsync(key, story, ct);
        return Results.Ok(new { source = "hacker-news", story });
    });
app.MapGet("/debug/best-stories", async (int n, BestStoriesRefresher refresher, CancellationToken ct) =>
{
    var snapshot = await refresher.GetOrLoadAsync(ct);
    return Results.Ok(new
    {
        snapshot.RefreshedAt,
        TotalCached = snapshot.Stories.Count,
        Stories = snapshot.Stories.Take(n)
    });
});

app.Run();

// Exposed so integration tests can start the app with WebApplicationFactory<Program>.
public partial class Program { }