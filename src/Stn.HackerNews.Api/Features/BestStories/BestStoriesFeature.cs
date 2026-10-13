using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stn.HackerNews.Api.Features.BestStories;

public static class BestStoriesFeature
{
    public static IServiceCollection AddBestStoriesFeature(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BestStoriesOptions>(configuration.GetSection(BestStoriesOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<BestStoriesRefresher>();

        return services;
    }
}