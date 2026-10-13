namespace Stn.HackerNews.Api.Features.BestStories;

public sealed class BestStoriesOptions
{
    public const string SectionName = "BestStories";

    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan FailureCooldown { get; set; } = TimeSpan.FromSeconds(10);
}