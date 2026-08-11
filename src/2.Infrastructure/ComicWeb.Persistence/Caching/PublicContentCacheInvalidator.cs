using ComicWeb.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Persistence.Caching;

public sealed class PublicContentCacheInvalidator : IPublicContentCacheInvalidator
{
    private readonly IPublicContentCache _cache;

    public PublicContentCacheInvalidator(IPublicContentCache cache)
    {
        _cache = cache;
    }

    public async Task InvalidateStoryAsync(string storySlug, CancellationToken cancellationToken = default)
    {
        var slug = storySlug.Trim().ToLowerInvariant();
        await InvalidateStoryListsAsync(cancellationToken);
        await _cache.RemoveAsync($"comicweb:v1:story:{slug}", cancellationToken);
    }

    public async Task InvalidateStoryAndChaptersAsync(string storySlug, CancellationToken cancellationToken = default)
    {
        var slug = storySlug.Trim().ToLowerInvariant();
        await InvalidateStoryAsync(storySlug, cancellationToken);
        await _cache.RemoveByPrefixAsync($"comicweb:v1:story:{slug}:chapters", cancellationToken);
        await _cache.RemoveByPrefixAsync($"comicweb:v1:story:{slug}:chapter:", cancellationToken);
    }

    public async Task InvalidateStoryListsAsync(CancellationToken cancellationToken = default)
    {
        await _cache.RemoveByPrefixAsync("comicweb:v1:stories:list:", cancellationToken);
    }
}
