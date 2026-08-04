using ComicWeb.Application.Common.Interfaces;
using System;
using System.Security.Cryptography;
using System.Text;

namespace ComicWeb.Persistence.Caching;

public sealed class PublicCacheKeyFactory : IPublicCacheKeyFactory
{
    public string CreateStoryListKey(int page, int pageSize, string? query, string? author, string sort)
    {
        var normalizedQuery = (query ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedAuthor = (author ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedSort = sort.Trim().ToLowerInvariant();

        var input = $"page:{page}|size:{pageSize}|q:{normalizedQuery}|a:{normalizedAuthor}|s:{normalizedSort}";
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"comicweb:v1:stories:list:{hash}";
    }

    public string CreateStoryDetailKey(string storySlug)
    {
        return $"comicweb:v1:story:{storySlug.Trim().ToLowerInvariant()}";
    }

    public string CreateChapterListKey(string storySlug, int page, int pageSize, string sort)
    {
        var normalizedSlug = storySlug.Trim().ToLowerInvariant();
        var normalizedSort = sort.Trim().ToLowerInvariant();
        
        var input = $"page:{page}|size:{pageSize}|s:{normalizedSort}";
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"comicweb:v1:story:{normalizedSlug}:chapters:list:{hash}";
    }

    public string CreateChapterDetailKey(string storySlug, string chapterSlug)
    {
        return $"comicweb:v1:story:{storySlug.Trim().ToLowerInvariant()}:chapter:{chapterSlug.Trim().ToLowerInvariant()}";
    }
}
