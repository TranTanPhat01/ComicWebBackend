using ComicWeb.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Persistence.Caching;

public sealed class PublicContentCacheInvalidator : IPublicContentCacheInvalidator
{
    private readonly IPublicContentCache _cache;
    private readonly ICdnCachePurger _cdnPurger;

    public PublicContentCacheInvalidator(IPublicContentCache cache, ICdnCachePurger cdnPurger)
    {
        _cache = cache;
        _cdnPurger = cdnPurger;
    }

    public async Task InvalidateStoryAsync(string storySlug, CancellationToken cancellationToken = default)
    {
        var slug = storySlug.Trim().ToLowerInvariant();
        await InvalidateStoryListsAsync(cancellationToken);
        await _cache.RemoveAsync($"comicweb:v1:story:{slug}", cancellationToken);

        // Purge CDN paths for story list and story detail
        await _cdnPurger.PurgeUrlsAsync(new[]
        {
            "/api/v1/stories",
            $"/api/v1/stories/{slug}"
        }, cancellationToken);
    }

    public async Task InvalidateStoryAndChaptersAsync(string storySlug, CancellationToken cancellationToken = default)
    {
        var slug = storySlug.Trim().ToLowerInvariant();
        await InvalidateStoryAsync(storySlug, cancellationToken);
        await _cache.RemoveByPrefixAsync($"comicweb:v1:story:{slug}:chapters", cancellationToken);
        await _cache.RemoveByPrefixAsync($"comicweb:v1:story:{slug}:chapter:", cancellationToken);

        // Purge CDN paths for chapters list and chapters under this story (using wildcard/prefix)
        await _cdnPurger.PurgeUrlsAsync(new[]
        {
            $"/api/v1/stories/{slug}/chapters",
            $"/api/v1/stories/{slug}/chapters/*"
        }, cancellationToken);
    }

    public async Task InvalidateStoryListsAsync(CancellationToken cancellationToken = default)
    {
        await _cache.RemoveByPrefixAsync("comicweb:v1:stories:list:", cancellationToken);
        await _cdnPurger.PurgeUrlsAsync(new[] { "/api/v1/stories" }, cancellationToken);
    }
}
