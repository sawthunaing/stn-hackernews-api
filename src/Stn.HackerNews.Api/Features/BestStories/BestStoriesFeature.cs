using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stn.HackerNews.Api.Features.BestStories;

/// <summary>Registers everything the Best Stories slice needs.</summary>
public static class BestStoriesFeature
{
    public static IServiceCollection AddBestStoriesFeature(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BestStoriesOptions>(configuration.GetSection(BestStoriesOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<BestStoriesRefresher>();
        services.AddSingleton<GetBestStoriesHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapBestStoriesEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGetBestStories();
}