using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Stn.HackerNews.Api.Infrastructure.Caching;
using Stn.HackerNews.Api.Infrastructure.HackerNews;

namespace Stn.HackerNews.Api.Features.BestStories;

/// <summary>
/// The only component that talks to Hacker News. It builds a pre-sorted snapshot of all
/// best stories and stores it in the cache. Calls to Hacker News depend on the refresh
/// interval, not on how much traffic this API receives.
/// </summary>
public sealed class BestStoriesRefresher(
    IHackerNewsClient client,
    ICacheStore cache,
    IOptions<BestStoriesOptions> options,
    IOptions<HackerNewsOptions> hackerNewsOptions,
    TimeProvider time,
    ILogger<BestStoriesRefresher> logger)
{
    public const string CacheKey = "best-stories:snapshot";

    // Request coalescing: only one load/refresh talks to Hacker News at a time.
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Last successfully fetched version of each story, used when a single item fails.
    // Only read and written while holding _lock.
    private IReadOnlyDictionary<int, StoryDto> _lastKnown = new Dictionary<int, StoryDto>();
    private DateTimeOffset? _lastFailureAt;

    public Task<BestStoriesSnapshot> GetOrLoadAsync(CancellationToken ct) =>
        RefreshIfOlderThanAsync(TimeSpan.MaxValue, ct);

    public async Task<BestStoriesSnapshot> RefreshIfOlderThanAsync(TimeSpan maxAge, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var now = time.GetUtcNow();
            var existing = await cache.GetAsync<BestStoriesSnapshot>(CacheKey, ct);

            // Double-check: another caller may have loaded it while we were waiting for the lock.
            if (existing is not null && now - existing.RefreshedAt < maxAge)
            {
                return existing;
            }

            // Don't let queued requests hammer Hacker News while it is failing.
            if (_lastFailureAt is { } failedAt && now - failedAt < options.Value.FailureCooldown)
            {
                return existing
                       ?? throw new StoriesUnavailableException("Hacker News recently failed; in cooldown.");
            }

            var snapshot = await FetchAsync(ct);
            await cache.SetAsync(CacheKey, snapshot, ct);
            return snapshot;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<BestStoriesSnapshot> FetchAsync(CancellationToken ct)
    {
        try
        {
            var ids = await client.GetBestStoryIdsAsync(ct);
            var previous = _lastKnown;
            var fetched = new ConcurrentDictionary<int, StoryDto>();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = hackerNewsOptions.Value.MaxConcurrentRequests,
                CancellationToken = ct
            };

            await Parallel.ForEachAsync(ids, parallelOptions, async (id, token) =>
            {
                try
                {
                    var item = await client.GetItemAsync(id, token);
                    if (item is { Deleted: false, Dead: false })
                    {
                        fetched[id] = StoryDto.FromItem(item);
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    // One bad item shouldn't fail the whole refresh: keep its last known version.
                    logger.LogWarning(ex, "Failed to fetch Hacker News item {Id}", id);
                    if (previous.TryGetValue(id, out var old))
                    {
                        fetched[id] = old;
                    }
                }
            });

            if (fetched.IsEmpty && ids.Count > 0)
            {
                throw new StoriesUnavailableException("No story details could be fetched.");
            }

            var stories = fetched.Values
                .OrderByDescending(s => s.Score)
                .ThenByDescending(s => s.Time)
                .ToArray();

            _lastKnown = fetched;
            _lastFailureAt = null;
            logger.LogInformation("Best stories refreshed: {Count} stories cached", stories.Length);

            return new BestStoriesSnapshot(stories, time.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _lastFailureAt = time.GetUtcNow();
            throw ex as StoriesUnavailableException
                  ?? new StoriesUnavailableException("Failed to load best stories.", ex);
        }
    }
}