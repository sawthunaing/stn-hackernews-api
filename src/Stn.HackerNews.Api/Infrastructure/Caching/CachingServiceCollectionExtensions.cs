namespace Stn.HackerNews.Api.Infrastructure.Caching;

public static class CachingServiceCollectionExtensions
{
    public static IServiceCollection AddAppCaching(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheStore, MemoryCacheStore>();
        return services;
    }
}