using ComicWeb.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ComicWeb.Persistence.Content;

/// <summary>
/// Background service that periodically flushes buffered view counts to the database.
/// Runs every 5 minutes. Safe to restart — counts in buffer will be flushed on next cycle.
/// </summary>
public sealed class ViewCountFlushWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ViewCountFlushWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ViewCountFlushWorker started. Flush interval: {Interval}.", FlushInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(FlushInterval, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IViewCountService>();
                var flushed = await service.FlushAsync(stoppingToken);
                if (flushed > 0)
                    logger.LogInformation("ViewCountFlushWorker: flushed {Count} story counts.", flushed);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ViewCountFlushWorker encountered an error during flush.");
            }
        }

        logger.LogInformation("ViewCountFlushWorker stopping. Performing final flush...");
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IViewCountService>();
            await service.FlushAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ViewCountFlushWorker final flush failed.");
        }
    }
}
