namespace Stn.HackerNews.Api.Infrastructure.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Maximum requests per client in each window.</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>Length of the fixed window in seconds.</summary>
    public int WindowSeconds { get; set; } = 10;
}