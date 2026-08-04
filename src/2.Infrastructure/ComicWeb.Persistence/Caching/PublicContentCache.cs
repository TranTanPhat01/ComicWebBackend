using ComicWeb.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Persistence.Caching;

public sealed class PublicContentCache : IPublicContentCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<PublicContentCache> _logger;
    private readonly ConcurrentDictionary<string, byte> _keys = new();

    public PublicContentCache(IMemoryCache memoryCache, ILogger<PublicContentCache> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out T? value))
            {
                return Task.FromResult(value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from public cache for key {Key}", key);
        }
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (value == null) return Task.CompletedTask;

        try
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };
            
            options.RegisterPostEvictionCallback((evictedKey, _, reason, _) =>
            {
                if (evictedKey is string k)
                {
                    _keys.TryRemove(k, out _);
                }
            });

            _memoryCache.Set(key, value, options);
            _keys[key] = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing to public cache for key {Key}", key);
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            _memoryCache.Remove(key);
            _keys.TryRemove(key, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing from public cache for key {Key}", key);
        }
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try
        {
            var matchedKeys = _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in matchedKeys)
            {
                _memoryCache.Remove(key);
                _keys.TryRemove(key, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing by prefix {Prefix} from public cache", prefix);
        }
        return Task.CompletedTask;
    }
}
