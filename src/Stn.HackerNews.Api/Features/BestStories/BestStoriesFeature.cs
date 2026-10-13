using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stn.HackerNews.Api.Features.BestStories;

/// <summary>Registers everything the Best Stories slice needs.</summary>
public static class BestStoriesFeature
{
    public static IServiceCollection AddBestStoriesFeature(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BestStoriesOptions>()
            .Bind(configuration.GetSection(BestStoriesOptions.SectionName))
            .Validate(o => o.RefreshInterval > TimeSpan.Zero, "BestStories:RefreshInterval must be positive.")
            .Validate(o => o.FailureCooldown >= TimeSpan.Zero, "BestStories:FailureCooldown cannot be negative.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<BestStoriesRefresher>();
        services.AddSingleton<GetBestStoriesHandler>();
        services.AddHostedService<BestStoriesRefreshService>();

        return services;
    }

    public static IEndpointRouteBuilder MapBestStoriesEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGetBestStories();
}