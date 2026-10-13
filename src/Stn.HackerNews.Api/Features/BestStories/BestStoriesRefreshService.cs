using Microsoft.Extensions.Options;

namespace Stn.HackerNews.Api.Features.BestStories;

public sealed class BestStoriesRefreshService(
    BestStoriesRefresher refresher,
    IOptions<BestStoriesOptions> options,
    TimeProvider time,
    ILogger<BestStoriesRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.RefreshInterval;

        using var timer = new PeriodicTimer(interval, time);

        await RunSafelyAsync(() => refresher.GetOrLoadAsync(stoppingToken), stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafelyAsync(
                () => refresher.RefreshIfOlderThanAsync(interval / 2, stoppingToken),
                stoppingToken);
        }
    }

    private async Task RunSafelyAsync(Func<Task> action, CancellationToken stoppingToken)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background refresh of best stories failed");
        }
    }
}