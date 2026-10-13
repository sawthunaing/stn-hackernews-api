using Stn.HackerNews.Api.Infrastructure.HackerNews;

namespace Stn.HackerNews.Tests.Shared;

internal sealed class FakeHackerNewsClient : IHackerNewsClient
{
    private int _bestStoryIdCalls;
    private int _inFlightItemRequests;
    private int _maxInFlightItemRequests;

    public Dictionary<int, HackerNewsItem?> Items { get; } = new();
    public HashSet<int> FailingIds { get; } = new();
    public bool FailBestStories { get; set; }
    public TimeSpan BestStoriesDelay { get; set; }
    public TimeSpan ItemDelay { get; set; }

    public int BestStoryIdCalls => Volatile.Read(ref _bestStoryIdCalls);
    public int MaxConcurrentItemRequests => Volatile.Read(ref _maxInFlightItemRequests);

    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _bestStoryIdCalls);
        if (BestStoriesDelay > TimeSpan.Zero) await Task.Delay(BestStoriesDelay, ct);
        if (FailBestStories) throw new HttpRequestException("Hacker News is down");
        return Items.Keys.ToArray();
    }

    public async Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken ct)
    {
        var inFlight = Interlocked.Increment(ref _inFlightItemRequests);
        UpdateMax(inFlight);
        try
        {
            if (ItemDelay > TimeSpan.Zero) await Task.Delay(ItemDelay, ct);
            if (FailingIds.Contains(id)) throw new HttpRequestException($"Item {id} failed");
            return Items.GetValueOrDefault(id);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlightItemRequests);
        }
    }

    public static HackerNewsItem Story(int id, int score, string? url = "default") => new()
    {
        Id = id,
        Type = "story",
        Title = $"Story {id}",
        By = $"user{id}",
        Url = url == "default" ? $"https://example.com/{id}" : url,
        Score = score,
        Time = 1570887781, // 2019-10-12T13:43:01+00:00
        Descendants = 572
    };

    private void UpdateMax(int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref _maxInFlightItemRequests)) &&
               Interlocked.CompareExchange(ref _maxInFlightItemRequests, value, current) != current)
        {
        }
    }
}