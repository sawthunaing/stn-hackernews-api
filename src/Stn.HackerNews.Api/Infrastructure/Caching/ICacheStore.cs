namespace Stn.HackerNews.Api.Infrastructure.Caching;

public interface ICacheStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;
    Task SetAsync<T>(string key, T value, CancellationToken ct) where T : class;
}