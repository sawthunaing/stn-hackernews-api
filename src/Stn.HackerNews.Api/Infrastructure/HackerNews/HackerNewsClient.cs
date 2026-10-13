namespace Stn.HackerNews.Api.Infrastructure.HackerNews;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct);
    Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken ct);
}

public sealed class HackerNewsClient(HttpClient httpClient) : IHackerNewsClient
{
    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct) =>
        await httpClient.GetFromJsonAsync<int[]>("beststories.json", ct) ?? [];
    public Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken ct) =>
        httpClient.GetFromJsonAsync<HackerNewsItem>($"item/{id}.json", ct);
}