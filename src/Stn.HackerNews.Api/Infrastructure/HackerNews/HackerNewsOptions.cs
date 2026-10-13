namespace Stn.HackerNews.Api.Infrastructure.HackerNews;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";

    public int MaxConcurrentRequests { get; set; } = 10;
}