using Stn.HackerNews.Api.Infrastructure.HackerNews;

namespace Stn.HackerNews.Api.Features.BestStories;

/// <summary>Story in the shape returned by this API.</summary>
public sealed record StoryDto(
    string Title,
    string Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount)
{
    public static StoryDto FromItem(HackerNewsItem item) => new(
        Title: item.Title ?? string.Empty,
        // Text posts (e.g. Ask HN) have no URL, so link to the discussion page instead.
        Uri: string.IsNullOrWhiteSpace(item.Url)
            ? $"https://news.ycombinator.com/item?id={item.Id}"
            : item.Url,
        PostedBy: item.By ?? string.Empty,
        Time: DateTimeOffset.FromUnixTimeSeconds(item.Time),
        Score: item.Score,
        CommentCount: item.Descendants ?? 0);
}