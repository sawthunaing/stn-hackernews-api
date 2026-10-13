namespace Stn.HackerNews.Api.Infrastructure.Caching;

public static class CachingServiceCollectionExtensions
{
    public static IServiceCollection AddAppCaching(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CachingOptions.SectionName);
        services.Configure<CachingOptions>(section);
        var options = section.Get<CachingOptions>() ?? new CachingOptions();

        services.AddMemoryCache();

        if (options.Provider == CacheProvider.Redis)
        {
            services.AddStackExchangeRedisCache(redis =>
            {
                redis.Configuration = options.RedisConnectionString;
                redis.InstanceName = "hn-best-stories:";
            });
            services.AddSingleton<ICacheStore, RedisCacheStore>();
        }
        else
        {
            services.AddSingleton<ICacheStore, MemoryCacheStore>();
        }

        return services;
    }
}