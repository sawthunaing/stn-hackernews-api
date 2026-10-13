namespace Stn.HackerNews.Api.Features.BestStories;

public sealed record BestStoriesSnapshot(IReadOnlyList<StoryDto> Stories, DateTimeOffset RefreshedAt);