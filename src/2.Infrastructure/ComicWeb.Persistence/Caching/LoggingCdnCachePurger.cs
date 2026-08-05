using ComicWeb.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Persistence.Caching;

public sealed class LoggingCdnCachePurger : ICdnCachePurger
{
    private readonly ILogger<LoggingCdnCachePurger> _logger;

    public LoggingCdnCachePurger(ILogger<LoggingCdnCachePurger> logger)
    {
        _logger = logger;
    }

    public Task PurgeUrlsAsync(IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        if (urls == null || urls.Count == 0)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("[CDN Purge] Requesting cache invalidation for URLs: {Urls}", string.Join(", ", urls));
        return Task.CompletedTask;
    }
}
