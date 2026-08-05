using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Interfaces;

public interface ICdnCachePurger
{
    Task PurgeUrlsAsync(IReadOnlyList<string> urls, CancellationToken cancellationToken = default);
}
