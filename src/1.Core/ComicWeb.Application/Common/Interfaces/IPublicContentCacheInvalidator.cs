using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Interfaces;

public interface IPublicContentCacheInvalidator
{
    Task InvalidateStoryAsync(string storySlug, CancellationToken cancellationToken = default);
    Task InvalidateStoryAndChaptersAsync(string storySlug, CancellationToken cancellationToken = default);
    Task InvalidateStoryListsAsync(CancellationToken cancellationToken = default);
}
