using ComicWeb.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Services;

public sealed class ScheduledPublishingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ScheduledPublishingWorker> _logger;
    private readonly ScheduledPublishingOptions _options;

    public ScheduledPublishingWorker(
        IServiceProvider serviceProvider,
        IHostApplicationLifetime lifetime,
        IOptions<ScheduledPublishingOptions> options,
        ILogger<ScheduledPublishingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _lifetime = lifetime;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Scheduled publishing worker is disabled by configuration.");
            return;
        }

        _logger.LogInformation("Scheduled publishing worker starting...");

        // Wait for the application host to fully start (database migrations & bootstrap run first)
        try
        {
            await Task.Yield();
            var hostStartedCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = _lifetime.ApplicationStarted.Register(() => hostStartedCompletionSource.TrySetResult());
            
            // Wait until the host is fully started or cancellation is requested
            await Task.WhenAny(hostStartedCompletionSource.Task, Task.Delay(Timeout.Infinite, stoppingToken));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scheduled publishing worker cancellation requested during host startup.");
            return;
        }

        _logger.LogInformation("Scheduled publishing worker is now active. Interval: {Interval} seconds.", _options.IntervalSeconds);

        var interval = TimeSpan.FromSeconds(_options.IntervalSeconds);
        using var timer = new PeriodicTimer(interval);

        try
        {
            // Execute once immediately on startup
            await RunPublishingCycleAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunPublishingCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scheduled publishing worker is stopping due to application shutdown.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Scheduled publishing worker encountered an unhandled exception and is stopping.");
        }
    }

    private async Task RunPublishingCycleAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting scheduled publishing iteration...");
        var startTime = DateTime.UtcNow;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var publishingService = scope.ServiceProvider.GetRequiredService<IScheduledPublishingService>();
            
            var result = await publishingService.PublishDueContentAsync(cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            if (result.StoriesPublished > 0 || result.ChaptersPublished > 0)
            {
                _logger.LogInformation(
                    "Scheduled publishing cycle completed in {Duration}ms. " +
                    "Stories: [Scanned: {SScan}, Published: {SPub}, Skipped: {SSkip}, Failed: {SFail}]. " +
                    "Chapters: [Scanned: {CScan}, Published: {CPub}, Skipped: {CSkip}, Failed: {CFail}].",
                    (int)duration.TotalMilliseconds,
                    result.StoriesScanned, result.StoriesPublished, result.StoriesSkipped, result.StoriesFailed,
                    result.ChaptersScanned, result.ChaptersPublished, result.ChaptersSkipped, result.ChaptersFailed);
            }
            else
            {
                _logger.LogDebug("Scheduled publishing cycle completed in {Duration}ms. No due content was found.", (int)duration.TotalMilliseconds);
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation to cleanly stop the worker
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during scheduled publishing execution cycle.");
        }
    }
}
