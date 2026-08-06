using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Stories;

public sealed record PageMeta(int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, PageMeta Meta);
public sealed record AdminStoryDto(int Id, string Title, string Slug, string Description, string CoverImageUrl, string? AuthorName, IReadOnlyList<string> Genres, string Status, int Version, DateTime? PublishedAt, DateTime? DeletedAt, DateTime CreateAt, DateTime? UpdateAt, DateTime? ScheduledAt = null);
public sealed record AdminChapterDto(int Id, int StoryId, int ChapterNumber, string Title, string Slug, string Content, string Status, int Version, DateTime? PublishedAt, DateTime? DeletedAt, DateTime CreateAt, DateTime? UpdateAt, bool IsLocked, string? AffiliateLink, DateTime? ScheduledAt = null);
public sealed record AdminStoryListQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Status = null, bool IncludeDeleted = false, string Sort = "createdAt", bool Desc = true) : IRequest<PagedResult<AdminStoryDto>>;
public sealed record AdminStoryByIdQuery(int Id, bool IncludeDeleted = false) : IRequest<AdminStoryDto>;
public sealed record CreateAdminStoryCommand(string Title, string? Slug, string Description, string? CoverImageUrl, string? AuthorName, IReadOnlyList<string>? Genres) : IRequest<AdminStoryDto>;
public sealed record UpdateAdminStoryCommand(int Id, string Title, string? Slug, string Description, string? CoverImageUrl, string? AuthorName, IReadOnlyList<string>? Genres, int Version) : IRequest<AdminStoryDto>;
public sealed record DeleteAdminStoryCommand(int Id, int Version) : IRequest;
public sealed record RestoreAdminStoryCommand(int Id, int Version) : IRequest<AdminStoryDto>;
public sealed record AdminChapterListQuery(int StoryId, int Page = 1, int PageSize = 20, string? Search = null, string? Status = null, bool IncludeDeleted = false, string Sort = "chapterNumber", bool Desc = false) : IRequest<PagedResult<AdminChapterDto>>;
public sealed record AdminChapterByIdQuery(int StoryId, int Id, bool IncludeDeleted = false) : IRequest<AdminChapterDto>;
public sealed record CreateAdminChapterCommand(int StoryId, int ChapterNumber, string Title, string? Slug, string Content, bool IsLocked = false, string? AffiliateLink = null) : IRequest<AdminChapterDto>;
public sealed record UpdateAdminChapterCommand(int StoryId, int Id, int ChapterNumber, string Title, string? Slug, string Content, int Version, bool IsLocked = false, string? AffiliateLink = null) : IRequest<AdminChapterDto>;
public sealed record DeleteAdminChapterCommand(int StoryId, int Id, int Version) : IRequest;
public sealed record RestoreAdminChapterCommand(int StoryId, int Id, int Version) : IRequest<AdminChapterDto>;

public sealed class AdminContentHandler :
    IRequestHandler<AdminStoryListQuery, PagedResult<AdminStoryDto>>,
    IRequestHandler<AdminStoryByIdQuery, AdminStoryDto>,
    IRequestHandler<CreateAdminStoryCommand, AdminStoryDto>,
    IRequestHandler<UpdateAdminStoryCommand, AdminStoryDto>,
    IRequestHandler<DeleteAdminStoryCommand>,
    IRequestHandler<RestoreAdminStoryCommand, AdminStoryDto>,
    IRequestHandler<AdminChapterListQuery, PagedResult<AdminChapterDto>>,
    IRequestHandler<AdminChapterByIdQuery, AdminChapterDto>,
    IRequestHandler<CreateAdminChapterCommand, AdminChapterDto>,
    IRequestHandler<UpdateAdminChapterCommand, AdminChapterDto>,
    IRequestHandler<DeleteAdminChapterCommand>,
    IRequestHandler<RestoreAdminChapterCommand, AdminChapterDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISlugGenerator _slugs;
    private readonly IHtmlContentSanitizer _sanitizer;
    private readonly IAuditWriter _auditWriter;
    private readonly IPublicContentCacheInvalidator _cacheInvalidator;

    public AdminContentHandler(
        IApplicationDbContext db,
        ISlugGenerator slugs,
        IHtmlContentSanitizer sanitizer,
        IAuditWriter auditWriter,
        IPublicContentCacheInvalidator cacheInvalidator)
    {
        _db = db;
        _slugs = slugs;
        _sanitizer = sanitizer;
        _auditWriter = auditWriter;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PagedResult<AdminStoryDto>> Handle(AdminStoryListQuery request, CancellationToken ct)
    {
        var (page, size) = Page(request.Page, request.PageSize);
        IQueryable<Story> query = request.IncludeDeleted ? _db.Stories.IgnoreQueryFilters() : _db.Stories;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(search) || x.Slug.ToLower().Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<StoryStatus>(request.Status, true, out var status)) throw Bad("INVALID_STATUS", "Status is invalid.");
            query = query.Where(x => x.Status == status);
        }
        query = SortStories(query, request.Sort, request.Desc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * size).Take(size).Select(x => StoryDto(x)).ToListAsync(ct);
        return new PagedResult<AdminStoryDto>(items, new PageMeta(page, size, total, (int)Math.Ceiling(total / (double)size)));
    }

    public async Task<AdminStoryDto> Handle(AdminStoryByIdQuery request, CancellationToken ct) => StoryDto(await Story(request.Id, request.IncludeDeleted, ct));

    public async Task<AdminStoryDto> Handle(CreateAdminStoryCommand request, CancellationToken ct)
    {
        ValidateStory(request.Title, request.Description, request.CoverImageUrl);
        var slug = await UniqueStorySlug(request.Slug, request.Title, ct);
        var story = new Story();
        story.UpdateDetails(request.Title, slug, request.Description, request.CoverImageUrl, request.AuthorName, DateTime.UtcNow);
        await ApplyGenresToStory(story, request.Genres, ct);
        _db.Stories.Add(story);
        
        try
        {
            await Save(ct);
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "DUPLICATE_RESOURCE";
            await _auditWriter.WriteAsync(new AuditEvent(
                Action: "STORY_CREATED",
                EntityType: "Story",
                EntityId: null,
                Result: "Failed",
                Details: new { slug = slug },
                ErrorCode: code
            ), ct);
            throw;
        }

        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "STORY_CREATED",
            EntityType: "Story",
            EntityId: story.Id.ToString(),
            Result: "Success",
            Details: new { slug = story.Slug, status = story.Status.ToString() }
        ), ct);

        await Save(ct);
        await _cacheInvalidator.InvalidateStoryListsAsync(ct);
        return StoryDto(story);
    }

    public async Task<AdminStoryDto> Handle(UpdateAdminStoryCommand request, CancellationToken ct)
    {
        ValidateStory(request.Title, request.Description, request.CoverImageUrl);
        var story = await Story(request.Id, false, ct);
        CheckVersion(story.Version, request.Version);

        var changedFields = new System.Collections.Generic.List<string>();
        if (story.Title != request.Title.Trim()) changedFields.Add("Title");
        if (story.Description != request.Description.Trim()) changedFields.Add("Description");
        if (story.CoverImageUrl != (request.CoverImageUrl?.Trim() ?? string.Empty)) changedFields.Add("CoverImageUrl");
        if (story.AuthorName != request.AuthorName?.Trim()) changedFields.Add("AuthorName");

        var details = new { changedFields, oldVersion = story.Version, newVersion = story.Version + 1 };

        var slug = await UniqueStorySlug(request.Slug, request.Title, ct, story.Id);
        if (story.Slug != slug) changedFields.Add("Slug");

        var oldSlug = story.Slug;
        story.UpdateDetails(request.Title, slug, request.Description, request.CoverImageUrl, request.AuthorName, DateTime.UtcNow);
        await ApplyGenresToStory(story, request.Genres, ct);

        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "STORY_UPDATED",
            EntityType: "Story",
            EntityId: story.Id.ToString(),
            Result: "Success",
            Details: details
        ), ct);

        try
        {
            await Save(ct);
            await _cacheInvalidator.InvalidateStoryAsync(oldSlug, ct);
            if (oldSlug != slug)
            {
                await _cacheInvalidator.InvalidateStoryAsync(slug, ct);
            }
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "CONCURRENCY_CONFLICT";
            await _auditWriter.WriteAsync(new AuditEvent(
                Action: "STORY_UPDATED",
                EntityType: "Story",
                EntityId: story.Id.ToString(),
                Result: "Failed",
                Details: details,
                ErrorCode: code
            ), ct);
            throw;
        }

        return StoryDto(story);
    }

    public async Task Handle(DeleteAdminStoryCommand request, CancellationToken ct)
    {
        var story = await Story(request.Id, false, ct);
        CheckVersion(story.Version, request.Version);

        var details = new { slug = story.Slug, version = story.Version };
        story.SoftDelete(DateTime.UtcNow);

        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "STORY_DELETED",
            EntityType: "Story",
            EntityId: story.Id.ToString(),
            Result: "Success",
            Details: details
        ), ct);

        try
        {
            await Save(ct);
            await _cacheInvalidator.InvalidateStoryAndChaptersAsync(story.Slug, ct);
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "CONCURRENCY_CONFLICT";
            await _auditWriter.WriteAsync(new AuditEvent(
                Action: "STORY_DELETED",
                EntityType: "Story",
                EntityId: story.Id.ToString(),
                Result: "Failed",
                Details: details,
                ErrorCode: code
            ), ct);
            throw;
        }
    }

    public async Task<AdminStoryDto> Handle(RestoreAdminStoryCommand request, CancellationToken ct)
    {
        var story = await Story(request.Id, true, ct);
        if (story.DeletedAt is null) throw Bad("STORY_NOT_DELETED", "Story is not deleted.");
        CheckVersion(story.Version, request.Version);
        if (await _db.Stories.AnyAsync(x => x.Id != story.Id && x.Slug == story.Slug, ct)) throw Bad("SLUG_CONFLICT", "Story slug is already in use.", 409);

        var details = new { slug = story.Slug, version = story.Version };
        story.Restore(DateTime.UtcNow);

        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "STORY_RESTORED",
            EntityType: "Story",
            EntityId: story.Id.ToString(),
            Result: "Success",
            Details: details
        ), ct);

        try
        {
            await Save(ct);
            await _cacheInvalidator.InvalidateStoryAndChaptersAsync(story.Slug, ct);
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "CONCURRENCY_CONFLICT";
            await _auditWriter.WriteAsync(new AuditEvent(
                Action: "STORY_RESTORED",
                EntityType: "Story",
                EntityId: story.Id.ToString(),
                Result: "Failed",
                Details: details,
                ErrorCode: code
            ), ct);
            throw;
        }

        return StoryDto(story);
    }

    public async Task<PagedResult<AdminChapterDto>> Handle(AdminChapterListQuery request, CancellationToken ct)
    {
        var (page, size) = Page(request.Page, request.PageSize);
        IQueryable<Chapter> query = request.IncludeDeleted ? _db.Chapters.IgnoreQueryFilters() : _db.Chapters;
        query = query.Where(x => x.StoryId == request.StoryId);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim().ToLower(); query = query.Where(x => x.Title!.ToLower().Contains(search) || x.Slug.ToLower().Contains(search)); }
        if (!string.IsNullOrWhiteSpace(request.Status)) { if (!Enum.TryParse<ChapterStatus>(request.Status, true, out var status)) throw Bad("INVALID_STATUS", "Status is invalid."); query = query.Where(x => x.Status == status); }
        query = SortChapters(query, request.Sort, request.Desc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * size).Take(size).Select(x => ChapterDto(x)).ToListAsync(ct);
        return new PagedResult<AdminChapterDto>(items, new PageMeta(page, size, total, (int)Math.Ceiling(total / (double)size)));
    }

    public async Task<AdminChapterDto> Handle(AdminChapterByIdQuery request, CancellationToken ct) => ChapterDto(await Chapter(request.StoryId, request.Id, request.IncludeDeleted, ct));
    public async Task<AdminChapterDto> Handle(CreateAdminChapterCommand request, CancellationToken ct)
    {
        ValidateChapter(request.ChapterNumber, request.Title, request.Content);
        var sanitized = _sanitizer.Sanitize(request.Content);
        if (!_sanitizer.IsMeaningful(sanitized))
        {
            throw Bad("CHAPTER_CONTENT_REQUIRED", "Chapter content is empty or meaningless after sanitization.");
        }
        var story = await Story(request.StoryId, false, ct);
        if (await _db.Chapters.AnyAsync(x => x.StoryId == request.StoryId && x.ChapterNumber == request.ChapterNumber, ct)) throw Bad("CHAPTER_NUMBER_CONFLICT", "Chapter number is already in use.", 409);
        var slug = await UniqueChapterSlug(request.StoryId, request.Slug, request.Title, ct);
        var chapter = new Chapter { StoryId = request.StoryId, IsLocked = request.IsLocked, AffiliateLink = request.AffiliateLink };
        chapter.UpdateContent(request.ChapterNumber, request.Title, slug, sanitized, DateTime.UtcNow);
        _db.Chapters.Add(chapter);
        
        try
        {
            await Save(ct);
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "DUPLICATE_RESOURCE";
            await _auditWriter.WriteAsync(new AuditEvent(
                Action: "CHAPTER_CREATED",
                EntityType: "Chapter",
                EntityId: null,
                Result: "Failed",
                Details: new { storyId = request.StoryId, chapterNumber = request.ChapterNumber, slug = slug },
                ErrorCode: code
            ), ct);
            throw;
        }

        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "CHAPTER_CREATED",
            EntityType: "Chapter",
            EntityId: chapter.Id.ToString(),
            Result: "Success",
            Details: new { storyId = chapter.StoryId, chapterNumber = chapter.ChapterNumber, slug = chapter.Slug }
        ), ct);

        await Save(ct);
        await _cacheInvalidator.InvalidateStoryAndChaptersAsync(story.Slug, ct);
        return ChapterDto(chapter);
    }

    public async Task<AdminChapterDto> Handle(UpdateAdminChapterCommand request, CancellationToken ct)
    {
        ValidateChapter(request.ChapterNumber, request.Title, request.Content);
        var sanitized = _sanitizer.Sanitize(request.Content);
        if (!_sanitizer.IsMeaningful(sanitized))
        {
            throw Bad("CHAPTER_CONTENT_REQUIRED", "Chapter content is empty or meaningless after sanitization.");
        }
        var chapter = await Chapter(request.StoryId, request.Id, false, ct);
        CheckVersion(chapter.Version, request.Version);
        if (await _db.Chapters.AnyAsync(x => x.StoryId == request.StoryId && x.ChapterNumber == request.ChapterNumber && x.Id != request.Id, ct)) throw Bad("CHAPTER_NUMBER_CONFLICT", "Chapter number is already in use.", 409);
        var slug = await UniqueChapterSlug(request.StoryId, request.Slug, request.Title, ct, chapter.Id);

        var changedFields = new System.Collections.Generic.List<string>();
        if (chapter.ChapterNumber != request.ChapterNumber) changedFields.Add("ChapterNumber");
        if (chapter.Title != request.Title.Trim()) changedFields.Add("Title");
        if (chapter.Slug != slug) changedFields.Add("Slug");
        if (chapter.IsLocked != request.IsLocked) changedFields.Add("IsLocked");
        if (chapter.AffiliateLink != request.AffiliateLink) changedFields.Add("AffiliateLink");
        
        var contentChanged = chapter.Content != sanitized;
        if (contentChanged) changedFields.Add("Content");

        var details = new
        {
            changedFields,
            contentChanged,
            oldContentLength = chapter.Content?.Length ?? 0,
            newContentLength = sanitized?.Length ?? 0,
            oldVersion = chapter.Version,
            newVersion = chapter.Version + 1
        };

        chapter.UpdateContent(request.ChapterNumber, request.Title, slug, sanitized!, DateTime.UtcNow);
        chapter.IsLocked = request.IsLocked;
        chapter.AffiliateLink = request.AffiliateLink;

        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "CHAPTER_UPDATED",
            EntityType: "Chapter",
            EntityId: chapter.Id.ToString(),
            Result: "Success",
            Details: details
        ), ct);

        var story = await _db.Stories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == chapter.StoryId, ct);

        try
        {
            await Save(ct);
            if (story != null)
            {
                await _cacheInvalidator.InvalidateStoryAndChaptersAsync(story.Slug, ct);
            }
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "CONCURRENCY_CONFLICT";
            await _auditWriter.WriteAsync(new AuditEvent(
                Action: "CHAPTER_UPDATED",
                EntityType: "Chapter",
                EntityId: chapter.Id.ToString(),
                Result: "Failed",
                Details: details,
                ErrorCode: code
            ), ct);
            throw;
        }

        return ChapterDto(chapter);
    }

    public async Task Handle(DeleteAdminChapterCommand request, CancellationToken ct)
    {
        var chapter = await Chapter(request.StoryId, request.Id, false, ct);
        CheckVersion(chapter.Version, request.Version);

        var details = new { storyId = chapter.StoryId, chapterNumber = chapter.ChapterNumber, version = chapter.Version };
        chapter.SoftDelete(DateTime.UtcNow);

        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "CHAPTER_DELETED",
            EntityType: "Chapter",
            EntityId: chapter.Id.ToString(),
            Result: "Success",
            Details: details
        ), ct);

        var story = await _db.Stories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == chapter.StoryId, ct);

        try
        {
            await Save(ct);
            if (story != null)
            {
                await _cacheInvalidator.InvalidateStoryAndChaptersAsync(story.Slug, ct);
            }
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "CONCURRENCY_CONFLICT";
            await _auditWriter.WriteAsync(new AuditEvent(
                Action: "CHAPTER_DELETED",
                EntityType: "Chapter",
                EntityId: chapter.Id.ToString(),
                Result: "Failed",
                Details: details,
                ErrorCode: code
            ), ct);
            throw;
        }
    }

    public async Task<AdminChapterDto> Handle(RestoreAdminChapterCommand request, CancellationToken ct)
    {
        var chapter = await Chapter(request.StoryId, request.Id, true, ct);
        if (chapter.DeletedAt is null) throw Bad("CHAPTER_NOT_DELETED", "Chapter is not deleted.");
        CheckVersion(chapter.Version, request.Version);
        if (await _db.Chapters.AnyAsync(x => x.StoryId == request.StoryId && x.Id != request.Id && (x.Slug == chapter.Slug || x.ChapterNumber == chapter.ChapterNumber), ct)) throw Bad("CHAPTER_CONFLICT", "Chapter slug or number is already in use.", 409);

        var details = new { storyId = chapter.StoryId, chapterNumber = chapter.ChapterNumber, version = chapter.Version };
        chapter.Restore(DateTime.UtcNow);

        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "CHAPTER_RESTORED",
            EntityType: "Chapter",
            EntityId: chapter.Id.ToString(),
            Result: "Success",
            Details: details
        ), ct);

        var story = await _db.Stories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == chapter.StoryId, ct);

        try
        {
            await Save(ct);
            if (story != null)
            {
                await _cacheInvalidator.InvalidateStoryAndChaptersAsync(story.Slug, ct);
            }
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "CONCURRENCY_CONFLICT";
            await _auditWriter.WriteAsync(new AuditEvent(
                Action: "CHAPTER_RESTORED",
                EntityType: "Chapter",
                EntityId: chapter.Id.ToString(),
                Result: "Failed",
                Details: details,
                ErrorCode: code
            ), ct);
            throw;
        }

        return ChapterDto(chapter);
    }

    private static IQueryable<Story> SortStories(IQueryable<Story> query, string sort, bool desc) => sort.ToLowerInvariant() switch
    {
        "title" => desc ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
        "updatedat" => desc ? query.OrderByDescending(x => x.UpdateAt) : query.OrderBy(x => x.UpdateAt),
        "createdat" => desc ? query.OrderByDescending(x => x.CreateAt) : query.OrderBy(x => x.CreateAt),
        _ => throw Bad("INVALID_SORT", "Sort must be title, createdAt, or updatedAt.")
    };

    private static IQueryable<Chapter> SortChapters(IQueryable<Chapter> query, string sort, bool desc) => sort.ToLowerInvariant() switch
    {
        "title" => desc ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
        "createdat" => desc ? query.OrderByDescending(x => x.CreateAt) : query.OrderBy(x => x.CreateAt),
        "chapternumber" => desc ? query.OrderByDescending(x => x.ChapterNumber) : query.OrderBy(x => x.ChapterNumber),
        _ => throw Bad("INVALID_SORT", "Sort must be chapterNumber, title, or createdAt.")
    };
    private async Task<Story> Story(int id, bool deleted, CancellationToken ct)
    {
        var query = deleted ? _db.Stories.IgnoreQueryFilters() : _db.Stories;
        return await query.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Bad("STORY_NOT_FOUND", "Story was not found.", 404);
    }

    private async Task<Chapter> Chapter(int storyId, int id, bool deleted, CancellationToken ct)
    {
        var query = deleted ? _db.Chapters.IgnoreQueryFilters() : _db.Chapters;
        return await query.SingleOrDefaultAsync(x => x.Id == id && x.StoryId == storyId, ct) ?? throw Bad("CHAPTER_NOT_FOUND", "Chapter was not found.", 404);
    }
    private async Task<string> UniqueStorySlug(string? requested, string title, CancellationToken ct, int? current = null)
    {
        var basis = _slugs.Generate(string.IsNullOrWhiteSpace(requested) ? title : requested);
        if (string.IsNullOrWhiteSpace(basis)) throw Bad("INVALID_SLUG", "Slug must contain letters or numbers.");
        for (var number = 1; ; number++)
        {
            var candidate = number == 1 ? basis : $"{basis}-{number}";
            if (!await _db.Stories.AnyAsync(x => x.Slug == candidate && x.Id != current, ct)) return candidate;
        }
    }

    private async Task<string> UniqueChapterSlug(int storyId, string? requested, string title, CancellationToken ct, int? current = null)
    {
        var basis = _slugs.Generate(string.IsNullOrWhiteSpace(requested) ? title : requested);
        if (string.IsNullOrWhiteSpace(basis)) throw Bad("INVALID_SLUG", "Slug must contain letters or numbers.");
        for (var number = 1; ; number++)
        {
            var candidate = number == 1 ? basis : $"{basis}-{number}";
            if (!await _db.Chapters.AnyAsync(x => x.StoryId == storyId && x.Slug == candidate && x.Id != current, ct)) return candidate;
        }
    }
    private static void ValidateStory(string title, string description, string? cover) { if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 250) throw Bad("INVALID_TITLE", "Title is required and must be 250 characters or fewer."); if (string.IsNullOrWhiteSpace(description)) throw Bad("INVALID_DESCRIPTION", "Description is required."); if (!string.IsNullOrWhiteSpace(cover) && (!Uri.TryCreate(cover, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))) throw Bad("INVALID_COVER_URL", "Cover image URL must be HTTP or HTTPS."); }
    private static void ValidateChapter(int number, string title, string content) { if (number <= 0) throw Bad("INVALID_CHAPTER_NUMBER", "Chapter number must be positive."); if (string.IsNullOrWhiteSpace(title)) throw Bad("INVALID_TITLE", "Title is required."); if (string.IsNullOrWhiteSpace(content)) throw Bad("INVALID_CONTENT", "Content is required."); }
    private static (int Page, int Size) Page(int page, int size) { if (page < 1 || size < 1 || size > 100) throw Bad("INVALID_PAGINATION", "Page must be positive and pageSize must be between 1 and 100."); return (page, size); }
    private static void CheckVersion(int actual, int supplied) { if (actual != supplied) throw Bad("CONCURRENCY_CONFLICT", "This record was changed by another request.", 409); }
    private async Task Save(CancellationToken ct) { try { await _db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw Bad("CONCURRENCY_CONFLICT", "This record was changed by another request.", 409); } }
    private static AppException Bad(string code, string detail, int status = 400) => new(code, status, status == 404 ? "Not found" : status == 409 ? "Conflict" : "Validation failed", detail);
    private static AdminStoryDto StoryDto(Story story) => new(story.Id, story.Title, story.Slug, story.Description, story.CoverImageUrl, story.AuthorName, story.Genres.OrderBy(x => x.Name).Select(x => x.Name).ToList(), story.Status.ToString(), story.Version, story.PublishedAt, story.DeletedAt, story.CreateAt, story.UpdateAt, story.ScheduledAt);
    private static AdminChapterDto ChapterDto(Chapter chapter) => new(chapter.Id, chapter.StoryId, chapter.ChapterNumber, chapter.Title ?? string.Empty, chapter.Slug, chapter.Content ?? string.Empty, chapter.Status.ToString(), chapter.Version, chapter.PublishedAt, chapter.DeletedAt, chapter.CreateAt, chapter.UpdateAt, chapter.IsLocked, chapter.AffiliateLink, chapter.ScheduledAt);

    private async Task ApplyGenresToStory(Story story, IReadOnlyList<string>? requestedGenres, CancellationToken ct)
    {
        story.Genres.Clear();
        if (requestedGenres == null || requestedGenres.Count == 0)
        {
            return;
        }

        var normalized = requestedGenres
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return;
        }

        var normalizedSlugs = normalized.Select(x => x.ToLowerInvariant().Replace(" ", "-")).ToList();
        var existingGenres = await _db.Genres.Where(x => normalizedSlugs.Contains(x.Slug)).ToListAsync(ct);
        var existingSlugs = existingGenres.Select(x => x.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in normalized)
        {
            var slug = name.ToLowerInvariant().Replace(" ", "-");
            if (existingSlugs.Contains(slug))
            {
                var existing = existingGenres.First(x => string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                }
                continue;
            }

            var genre = new Genre { Name = name, Slug = slug, IsActive = true };
            _db.Genres.Add(genre);
            existingGenres.Add(genre);
            existingSlugs.Add(slug);
        }

        foreach (var genre in existingGenres)
        {
            story.Genres.Add(genre);
        }
    }
}
