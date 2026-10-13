using Microsoft.Extensions.Options;

namespace Stn.HackerNews.Api.Infrastructure.HackerNews;

public static class HackerNewsServiceCollectionExtensions
{
    public static IServiceCollection AddHackerNewsClient(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HackerNewsOptions>(configuration.GetSection(HackerNewsOptions.SectionName));

        services
            .AddHttpClient<IHackerNewsClient, HackerNewsClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            })
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
            .AddStandardResilienceHandler();

        return services;
    }
}