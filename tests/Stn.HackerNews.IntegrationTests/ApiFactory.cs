using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stn.HackerNews.Api.Infrastructure.Caching;
using Stn.HackerNews.Api.Infrastructure.HackerNews;
using Stn.HackerNews.Api.Infrastructure.RateLimiting;
using Stn.HackerNews.Tests.Shared;

namespace Stn.HackerNews.IntegrationTests;

/// <summary>
/// Starts the real application in memory with Hacker News replaced by a fake.
/// Configure <see cref="HackerNews"/> and <see cref="RateLimitPermits"/> before calling CreateClient().
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakeHackerNewsClient HackerNews { get; } = new();

    /// <summary>High by default so only the rate limiting tests are affected by it.</summary>
    public int RateLimitPermits { get; set; } = 1000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IHackerNewsClient>(HackerNews);

            // Each test gets its own in-memory cache, whatever the environment says
            // (e.g. Caching__Provider=Redis), so tests never share state.
            services.RemoveAll<ICacheStore>();
            services.AddSingleton<ICacheStore, MemoryCacheStore>();

            services.Configure<RateLimitingOptions>(options =>
            {
                options.PermitLimit = RateLimitPermits;
                options.WindowSeconds = 60;
            });
        });
    }
}