using System;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Interfaces;

public interface IPublicContentCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);
    
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
