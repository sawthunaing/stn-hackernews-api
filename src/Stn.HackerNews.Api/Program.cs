using Stn.HackerNews.Api.Infrastructure.HackerNews;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHackerNewsClient(builder.Configuration);

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

app.Run();

// Exposed so integration tests can start the app with WebApplicationFactory<Program>.
public partial class Program { }