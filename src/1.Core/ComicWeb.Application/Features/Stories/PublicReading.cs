using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Common.Caching;
using ComicWeb.Application.Common.Configurations;
using Microsoft.Extensions.Options;

namespace ComicWeb.Application.Features.Stories;

public sealed record PublicChapterSummaryDto(int Id, string Slug, int Number, string Title, DateTime? PublishedAt);
public sealed record PublicStoryListItemDto(int Id, string Slug, string Title, string Description, string CoverUrl, string? AuthorName, string Status, IReadOnlyList<string> Genres, int ChapterCount, PublicChapterSummaryDto? LatestChapter, DateTime? PublishedAt, DateTime? UpdatedAt);
public sealed record PublicStoryDetailDto(int Id, string Slug, string Title, string Description, string CoverUrl, string? AuthorName, string Status, IReadOnlyList<string> Genres, DateTime? PublishedAt, DateTime? UpdatedAt, IReadOnlyList<PublicChapterSummaryDto> Chapters, int Version);
public sealed record PublicStoryReferenceDto(int Id, string Slug, string Title);
public sealed record ChapterNavigationDto(string Slug, int Number, string Title);
public sealed record PublicChapterDetailDto(int Id, PublicStoryReferenceDto Story, string Slug, int Number, string Title, string Content, ChapterNavigationDto? PreviousChapter, ChapterNavigationDto? NextChapter, DateTime? PublishedAt, bool IsLocked, string? AffiliateLink, int Version, DateTime? UpdatedAt);
public sealed record CursorPagedResult<T>(IReadOnlyList<T> Items, int? NextCursor, bool HasMore);
public sealed record GenreListItemDto(int Id, string Name, string Slug, bool IsActive, int StoryCount);
public sealed record GetPublishedStoriesQuery(int Page = 1, int PageSize = 20, string? Query = null, string? Author = null, string? Genre = null, string Sort = "-updatedAt") : IRequest<PagedResult<PublicStoryListItemDto>>;
public sealed record GetGenresQuery() : IRequest<IReadOnlyList<GenreListItemDto>>;
public sealed record GetPublishedStoryBySlugQuery(string Slug) : IRequest<PublicStoryDetailDto>;
public sealed record GetPublishedChaptersByStorySlugQuery(string StorySlug, int? LastChapterNumber = null, int PageSize = 100) : IRequest<CursorPagedResult<PublicChapterSummaryDto>>;
public sealed record GetPublishedChapterBySlugQuery(string StorySlug, string ChapterSlug) : IRequest<PublicChapterDetailDto>;

internal sealed class SafePublicContentCache : IPublicContentCache
{
    private readonly IPublicContentCache _inner;

    public SafePublicContentCache(IPublicContentCache inner)
    {
        _inner = inner;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try { return await _inner.GetAsync<T>(key, cancellationToken); }
        catch { return default; }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try { await _inner.SetAsync(key, value, ttl, cancellationToken); }
        catch { }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try { await _inner.RemoveAsync(key, cancellationToken); }
        catch { }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try { await _inner.RemoveByPrefixAsync(prefix, cancellationToken); }
        catch { }
    }
}

public sealed class GetPublishedStoriesQueryHandler : IRequestHandler<GetPublishedStoriesQuery, PagedResult<PublicStoryListItemDto>>
{
    private readonly IReadOnlyApplicationDbContext db;
    private readonly IPublicContentCache cache;
    private readonly IPublicCacheKeyFactory keyFactory;
    private readonly KeyedLockManager lockManager;
    private readonly IOptions<PublicCacheOptions> options;

    public GetPublishedStoriesQueryHandler(
        IReadOnlyApplicationDbContext db,
        IPublicContentCache cache,
        IPublicCacheKeyFactory keyFactory,
        KeyedLockManager lockManager,
        IOptions<PublicCacheOptions> options)
    {
        this.db = db;
        this.cache = new SafePublicContentCache(cache);
        this.keyFactory = keyFactory;
        this.lockManager = lockManager;
        this.options = options;
    }

    public async Task<PagedResult<PublicStoryListItemDto>> Handle(GetPublishedStoriesQuery request, CancellationToken ct)
    {
        ValidatePagination(request.Page, request.PageSize);
        var opt = options.Value;
        if (!opt.Enabled)
        {
            return await GetStoriesFromDb(request, ct);
        }

        var cacheKey = keyFactory.CreateStoryListKey(request.Page, request.PageSize, request.Query, request.Author, request.Sort);
        var cached = await cache.GetAsync<PagedResult<PublicStoryListItemDto>>(cacheKey, ct);
        if (cached != null) return cached;

        using (await lockManager.LockAsync(cacheKey))
        {
            cached = await cache.GetAsync<PagedResult<PublicStoryListItemDto>>(cacheKey, ct);
            if (cached != null) return cached;

            var data = await GetStoriesFromDb(request, ct);
            await cache.SetAsync(cacheKey, data, TimeSpan.FromSeconds(opt.StoryListTtlSeconds), ct);
            return data;
        }
    }

    private async Task<PagedResult<PublicStoryListItemDto>> GetStoriesFromDb(GetPublishedStoriesQuery request, CancellationToken ct)
    {
        var query = BuildPublicStoriesQuery(request);
        var total = await query.CountAsync(ct);
        var stories = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(ProjectStoryListItem()).ToListAsync(ct);
        return new(stories, CreatePageMeta(request.Page, request.PageSize, total));
    }

    private IQueryable<Story> BuildPublicStoriesQuery(GetPublishedStoriesQuery request)
    {
        var query = db.Stories.AsNoTracking().Where(IsPublicStory());
        if (!string.IsNullOrWhiteSpace(request.Query)) 
        { 
            var value = request.Query.Trim().ToLower();
            if (db is DbContext dbContext && dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                query = query.Where(x => x.Title.ToLower().Contains(value) || x.Slug.ToLower().Contains(value) || (x.AuthorName ?? "").ToLower().Contains(value));
            }
            else
            {
                var valueLike = $"%{request.Query.Trim()}%"; 
                query = query.Where(x => EF.Functions.ILike(x.Title, valueLike) || x.Slug.ToLower().Contains(value) || EF.Functions.ILike(x.AuthorName ?? "", valueLike)); 
            }
        }
        if (!string.IsNullOrWhiteSpace(request.Author)) 
        { 
            var value = request.Author.Trim().ToLower();
            if (db is DbContext dbContext && dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                query = query.Where(x => (x.AuthorName ?? "").ToLower().Contains(value));
            }
            else
            {
                var valueLike = $"%{request.Author.Trim()}%"; 
                query = query.Where(x => EF.Functions.ILike(x.AuthorName ?? "", valueLike)); 
            }
        }
        if (!string.IsNullOrWhiteSpace(request.Genre)) { var value = request.Genre.Trim(); query = query.Where(x => x.Genres.Any(g => g.Name == value || g.Slug == value)); }
        return request.Sort switch { "title" => query.OrderBy(x => x.Title), "-title" => query.OrderByDescending(x => x.Title), "updatedAt" => query.OrderBy(x => x.UpdateAt), "-updatedAt" => query.OrderByDescending(x => x.UpdateAt), "publishedAt" => query.OrderBy(x => x.PublishedAt), "-publishedAt" => query.OrderByDescending(x => x.PublishedAt), _ => throw InvalidSort() };
    }

    private static System.Linq.Expressions.Expression<Func<Story, bool>> IsPublicStory() => x => x.Status == StoryStatus.Published || x.Status == StoryStatus.Completed;
    private static System.Linq.Expressions.Expression<Func<Story, PublicStoryListItemDto>> ProjectStoryListItem() => x => new(x.Id, x.Slug, x.Title, x.Description, x.CoverImageUrl, x.AuthorName, x.Status.ToString(), x.Genres.Where(g => g.IsActive).OrderBy(g => g.Name).Select(g => g.Name).ToList(), x.Chapters.Count(c => c.Status == ChapterStatus.Published), x.Chapters.Where(c => c.Status == ChapterStatus.Published).OrderByDescending(c => c.ChapterNumber).Select(c => new PublicChapterSummaryDto(c.Id, c.Slug, c.ChapterNumber, c.Title!, c.PublishedAt)).FirstOrDefault(), x.PublishedAt, x.UpdateAt);
    private static void ValidatePagination(int page, int pageSize) { if (page < 1 || pageSize is < 1 or > 100) throw new AppException("INVALID_PAGE", 400, "Validation failed", "Invalid pagination."); }
    private static PageMeta CreatePageMeta(int page, int pageSize, int total) => new(page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    private static AppException InvalidSort() => new("INVALID_SORT", 400, "Validation failed", "Invalid sort.");
}

public sealed class GetGenresQueryHandler : IRequestHandler<GetGenresQuery, IReadOnlyList<GenreListItemDto>>
{
    private readonly IReadOnlyApplicationDbContext db;

    public GetGenresQueryHandler(IReadOnlyApplicationDbContext db)
    {
        this.db = db;
    }

    public async Task<IReadOnlyList<GenreListItemDto>> Handle(GetGenresQuery request, CancellationToken ct)
    {
        return await db.Genres.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new GenreListItemDto(x.Id, x.Name, x.Slug, x.IsActive, x.Stories.Count(s => s.Status == StoryStatus.Published || s.Status == StoryStatus.Completed)))
            .ToListAsync(ct);
    }
}

public sealed class GetPublishedStoryBySlugQueryHandler : IRequestHandler<GetPublishedStoryBySlugQuery, PublicStoryDetailDto>
{
    private readonly IReadOnlyApplicationDbContext db;
    private readonly IPublicContentCache cache;
    private readonly IPublicCacheKeyFactory keyFactory;
    private readonly KeyedLockManager lockManager;
    private readonly IOptions<PublicCacheOptions> options;

    public GetPublishedStoryBySlugQueryHandler(
        IReadOnlyApplicationDbContext db,
        IPublicContentCache cache,
        IPublicCacheKeyFactory keyFactory,
        KeyedLockManager lockManager,
        IOptions<PublicCacheOptions> options)
    {
        this.db = db;
        this.cache = new SafePublicContentCache(cache);
        this.keyFactory = keyFactory;
        this.lockManager = lockManager;
        this.options = options;
    }

    public async Task<PublicStoryDetailDto> Handle(GetPublishedStoryBySlugQuery request, CancellationToken ct)
    {
        var opt = options.Value;
        if (!opt.Enabled)
        {
            return await GetStoryDetailFromDb(request.Slug, ct);
        }

        var cacheKey = keyFactory.CreateStoryDetailKey(request.Slug);
        var cached = await cache.GetAsync<PublicStoryDetailDto>(cacheKey, ct);
        if (cached != null) return cached;

        using (await lockManager.LockAsync(cacheKey))
        {
            cached = await cache.GetAsync<PublicStoryDetailDto>(cacheKey, ct);
            if (cached != null) return cached;

            var data = await GetStoryDetailFromDb(request.Slug, ct);
            await cache.SetAsync(cacheKey, data, TimeSpan.FromSeconds(opt.StoryDetailTtlSeconds), ct);
            return data;
        }
    }

    private async Task<PublicStoryDetailDto> GetStoryDetailFromDb(string slug, CancellationToken ct)
    {
        var story = await db.Stories.AsNoTracking().Where(IsPublicStory()).Where(x => x.Slug == slug).Select(ProjectStoryDetail()).SingleOrDefaultAsync(ct);
        return story ?? throw NotFound("STORY_NOT_FOUND", "Story was not found.");
    }

    private static System.Linq.Expressions.Expression<Func<Story, bool>> IsPublicStory() => x => x.Status == StoryStatus.Published || x.Status == StoryStatus.Completed;
    private static System.Linq.Expressions.Expression<Func<Story, PublicStoryDetailDto>> ProjectStoryDetail() => x => new(x.Id, x.Slug, x.Title, x.Description, x.CoverImageUrl, x.AuthorName, x.Status.ToString(), x.Genres.Where(g => g.IsActive).OrderBy(g => g.Name).Select(g => g.Name).ToList(), x.PublishedAt, x.UpdateAt, x.Chapters.Where(c => c.Status == ChapterStatus.Published).OrderBy(c => c.ChapterNumber).Select(c => new PublicChapterSummaryDto(c.Id, c.Slug, c.ChapterNumber, c.Title!, c.PublishedAt)).ToList(), x.Version);
    private static AppException NotFound(string code, string detail) => new(code, 404, "Not found", detail);
}

public sealed class GetPublishedChaptersByStorySlugQueryHandler : IRequestHandler<GetPublishedChaptersByStorySlugQuery, CursorPagedResult<PublicChapterSummaryDto>>
{
    private readonly IReadOnlyApplicationDbContext db;
    private readonly IPublicContentCache cache;
    private readonly IPublicCacheKeyFactory keyFactory;
    private readonly KeyedLockManager lockManager;
    private readonly IOptions<PublicCacheOptions> options;

    public GetPublishedChaptersByStorySlugQueryHandler(
        IReadOnlyApplicationDbContext db,
        IPublicContentCache cache,
        IPublicCacheKeyFactory keyFactory,
        KeyedLockManager lockManager,
        IOptions<PublicCacheOptions> options)
    {
        this.db = db;
        this.cache = new SafePublicContentCache(cache);
        this.keyFactory = keyFactory;
        this.lockManager = lockManager;
        this.options = options;
    }

    public async Task<CursorPagedResult<PublicChapterSummaryDto>> Handle(GetPublishedChaptersByStorySlugQuery request, CancellationToken ct)
    {
        if (request.PageSize is < 1 or > 100) throw new AppException("INVALID_PAGE_SIZE", 400, "Validation failed", "Invalid page size.");
        var opt = options.Value;
        if (!opt.Enabled)
        {
            return await GetChaptersFromDb(request, ct);
        }

        var cacheKey = keyFactory.CreateChapterListKey(request.StorySlug, request.LastChapterNumber, request.PageSize);
        var cached = await cache.GetAsync<CursorPagedResult<PublicChapterSummaryDto>>(cacheKey, ct);
        if (cached != null) return cached;

        using (await lockManager.LockAsync(cacheKey))
        {
            cached = await cache.GetAsync<CursorPagedResult<PublicChapterSummaryDto>>(cacheKey, ct);
            if (cached != null) return cached;

            var data = await GetChaptersFromDb(request, ct);
            await cache.SetAsync(cacheKey, data, TimeSpan.FromSeconds(opt.ChapterListTtlSeconds), ct);
            return data;
        }
    }

    private async Task<CursorPagedResult<PublicChapterSummaryDto>> GetChaptersFromDb(GetPublishedChaptersByStorySlugQuery request, CancellationToken ct)
    {
        var story = await FindPublicStory(request.StorySlug, ct);
        var query = db.Chapters.AsNoTracking().Where(x => x.StoryId == story.Id && x.Status == ChapterStatus.Published);
        if (request.LastChapterNumber.HasValue)
        {
            query = query.Where(x => x.ChapterNumber > request.LastChapterNumber.Value);
        }
        query = query.OrderBy(x => x.ChapterNumber);
        
        var limit = request.PageSize + 1;
        var chapters = await query.Take(limit).Select(ProjectChapterSummary()).ToListAsync(ct);
        
        var hasMore = chapters.Count > request.PageSize;
        var items = hasMore ? chapters.Take(request.PageSize).ToList() : chapters;
        int? nextCursor = hasMore && items.Any() ? items.Last().Number : null;
        
        return new CursorPagedResult<PublicChapterSummaryDto>(items, nextCursor, hasMore);
    }

    private async Task<Story> FindPublicStory(string slug, CancellationToken ct) => await db.Stories.AsNoTracking().Where(IsPublicStory()).SingleOrDefaultAsync(x => x.Slug == slug, ct) ?? throw NotFound("STORY_NOT_FOUND", "Story was not found.");
    private static System.Linq.Expressions.Expression<Func<Story, bool>> IsPublicStory() => x => x.Status == StoryStatus.Published || x.Status == StoryStatus.Completed;
    private static System.Linq.Expressions.Expression<Func<Chapter, PublicChapterSummaryDto>> ProjectChapterSummary() => x => new(x.Id, x.Slug, x.ChapterNumber, x.Title!, x.PublishedAt);
    private static AppException NotFound(string code, string detail) => new(code, 404, "Not found", detail);
}

public sealed class GetPublishedChapterBySlugQueryHandler : IRequestHandler<GetPublishedChapterBySlugQuery, PublicChapterDetailDto>
{
    private readonly IReadOnlyApplicationDbContext db;
    private readonly IPublicContentCache cache;
    private readonly IPublicCacheKeyFactory keyFactory;
    private readonly KeyedLockManager lockManager;
    private readonly IOptions<PublicCacheOptions> options;

    public GetPublishedChapterBySlugQueryHandler(
        IReadOnlyApplicationDbContext db,
        IPublicContentCache cache,
        IPublicCacheKeyFactory keyFactory,
        KeyedLockManager lockManager,
        IOptions<PublicCacheOptions> options)
    {
        this.db = db;
        this.cache = new SafePublicContentCache(cache);
        this.keyFactory = keyFactory;
        this.lockManager = lockManager;
        this.options = options;
    }

    public async Task<PublicChapterDetailDto> Handle(GetPublishedChapterBySlugQuery request, CancellationToken ct)
    {
        var opt = options.Value;
        if (!opt.Enabled)
        {
            return await GetChapterDetailFromDb(request.StorySlug, request.ChapterSlug, ct);
        }

        var cacheKey = keyFactory.CreateChapterDetailKey(request.StorySlug, request.ChapterSlug);
        var cached = await cache.GetAsync<PublicChapterDetailDto>(cacheKey, ct);
        if (cached != null) return cached;

        using (await lockManager.LockAsync(cacheKey))
        {
            cached = await cache.GetAsync<PublicChapterDetailDto>(cacheKey, ct);
            if (cached != null) return cached;

            var data = await GetChapterDetailFromDb(request.StorySlug, request.ChapterSlug, ct);
            await cache.SetAsync(cacheKey, data, TimeSpan.FromSeconds(opt.ChapterDetailTtlSeconds), ct);
            return data;
        }
    }

    private async Task<PublicChapterDetailDto> GetChapterDetailFromDb(string storySlug, string chapterSlug, CancellationToken ct)
    {
        var chapter = await db.Chapters.AsNoTracking().Include(x => x.Story).SingleOrDefaultAsync(
            x => x.Slug == chapterSlug && x.Story.Slug == storySlug && x.Status == ChapterStatus.Published && (x.Story.Status == StoryStatus.Published || x.Story.Status == StoryStatus.Completed), ct);
        if (chapter is null) throw NotFound("CHAPTER_NOT_FOUND", "Chapter was not found.");
        var previous = await FindNavigation(chapter.StoryId, chapter.ChapterNumber, true, ct);
        var next = await FindNavigation(chapter.StoryId, chapter.ChapterNumber, false, ct);
        
        // Automatically lock chapters starting from Chapter 2
        bool isLocked = chapter.IsLocked || chapter.ChapterNumber >= 2;
        
        // Resolve affiliate link: use chapter affiliate link, fall back to global link if empty
        string? affiliateLink = chapter.AffiliateLink;
        if (string.IsNullOrWhiteSpace(affiliateLink))
        {
            var globalSetting = await db.SystemSettings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == "GlobalAffiliateLink", ct);
            affiliateLink = globalSetting?.Value;
        }

        // Safety fallback if no Shopee link is configured anywhere
        if (string.IsNullOrWhiteSpace(affiliateLink))
        {
            affiliateLink = "https://shopee.vn";
        }

        return new(chapter.Id, new(chapter.Story.Id, chapter.Story.Slug, chapter.Story.Title), chapter.Slug, chapter.ChapterNumber, chapter.Title!, chapter.Content!, previous, next, chapter.PublishedAt, isLocked, affiliateLink, chapter.Version, chapter.UpdateAt);
    }

    private async Task<ChapterNavigationDto?> FindNavigation(int storyId, int number, bool previous, CancellationToken ct) { var q = db.Chapters.AsNoTracking().Where(x => x.StoryId == storyId && x.Status == ChapterStatus.Published); q = previous ? q.Where(x => x.ChapterNumber < number).OrderByDescending(x => x.ChapterNumber) : q.Where(x => x.ChapterNumber > number).OrderBy(x => x.ChapterNumber); return await q.Select(x => new ChapterNavigationDto(x.Slug, x.ChapterNumber, x.Title!)).FirstOrDefaultAsync(ct); }
    private static AppException NotFound(string code, string detail) => new(code, 404, "Not found", detail);
}
