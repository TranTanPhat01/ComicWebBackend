using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Stories;

public sealed record PublishStoryCommand(int Id, int Version) : IRequest<PublicationDto>;
public sealed record UnpublishStoryCommand(int Id, int Version) : IRequest<PublicationDto>;
public sealed record HideStoryCommand(int Id, int Version) : IRequest<PublicationDto>;
public sealed record CompleteStoryCommand(int Id, int Version) : IRequest<PublicationDto>;
public sealed record PublishChapterCommand(int Id, int Version) : IRequest<PublicationDto>;
public sealed record UnpublishChapterCommand(int Id, int Version) : IRequest<PublicationDto>;
public sealed record HideChapterCommand(int Id, int Version) : IRequest<PublicationDto>;
public sealed record ScheduleStoryCommand(int Id, DateTime? ScheduledAt, int Version) : IRequest<AdminStoryDto>;
public sealed record ScheduleChapterCommand(int Id, DateTime? ScheduledAt, int Version) : IRequest<AdminChapterDto>;
public sealed record PublicationDto(int Id, string Status, DateTime? PublishedAt, int Version);

public sealed class PublishWorkflowHandler :
    IRequestHandler<PublishStoryCommand, PublicationDto>, IRequestHandler<UnpublishStoryCommand, PublicationDto>, IRequestHandler<HideStoryCommand, PublicationDto>, IRequestHandler<CompleteStoryCommand, PublicationDto>,
    IRequestHandler<PublishChapterCommand, PublicationDto>, IRequestHandler<UnpublishChapterCommand, PublicationDto>, IRequestHandler<HideChapterCommand, PublicationDto>,
    IRequestHandler<ScheduleStoryCommand, AdminStoryDto>, IRequestHandler<ScheduleChapterCommand, AdminChapterDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly IAuditWriter _auditWriter;
    private readonly IPublicContentCacheInvalidator _cacheInvalidator;

    public PublishWorkflowHandler(
        IApplicationDbContext db,
        IDateTimeProvider dateTime,
        IAuditWriter auditWriter,
        IPublicContentCacheInvalidator cacheInvalidator)
    {
        _db = db;
        _dateTime = dateTime;
        _auditWriter = auditWriter;
        _cacheInvalidator = cacheInvalidator;
    }

    public Task<PublicationDto> Handle(PublishStoryCommand request, CancellationToken ct) => ChangeStory(request.Id, request.Version, "publish", ct);
    public Task<PublicationDto> Handle(UnpublishStoryCommand request, CancellationToken ct) => ChangeStory(request.Id, request.Version, "unpublish", ct);
    public Task<PublicationDto> Handle(HideStoryCommand request, CancellationToken ct) => ChangeStory(request.Id, request.Version, "hide", ct);
    public Task<PublicationDto> Handle(CompleteStoryCommand request, CancellationToken ct) => ChangeStory(request.Id, request.Version, "complete", ct);
    public Task<PublicationDto> Handle(PublishChapterCommand request, CancellationToken ct) => ChangeChapter(request.Id, request.Version, "publish", ct);
    public Task<PublicationDto> Handle(UnpublishChapterCommand request, CancellationToken ct) => ChangeChapter(request.Id, request.Version, "unpublish", ct);
    public Task<PublicationDto> Handle(HideChapterCommand request, CancellationToken ct) => ChangeChapter(request.Id, request.Version, "hide", ct);

    public async Task<AdminStoryDto> Handle(ScheduleStoryCommand request, CancellationToken ct)
    {
        var story = await _db.Stories.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == request.Id, ct) ?? throw Error("STORY_NOT_FOUND", 404, "Story was not found.");
        if (story.DeletedAt is not null) throw Error("STORY_DELETED", 400, "Deleted story cannot change publication state.");
        
        if (story.Status != StoryStatus.Draft)
        {
            throw Error("INVALID_STATUS_TRANSITION", 400, "Only draft stories can be scheduled.");
        }
        
        CheckVersion(story.Version, request.Version);
        story.Schedule(request.ScheduledAt, _dateTime.UtcNow);

        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "STORY_SCHEDULED",
            EntityType: "Story",
            EntityId: story.Id.ToString(),
            Result: "Success",
            Details: new { scheduledAt = request.ScheduledAt }
        ), ct);

        try
        {
            await Save(ct);
            await _cacheInvalidator.InvalidateStoryAsync(story.Slug, ct);
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "CONCURRENCY_CONFLICT";
            await _auditWriter.WriteAsync(new AuditEvent(
                Action: "STORY_SCHEDULED",
                EntityType: "Story",
                EntityId: story.Id.ToString(),
                Result: "Failed",
                Details: new { scheduledAt = request.ScheduledAt },
                ErrorCode: code
            ), ct);
            throw;
        }

        return StoryDto(story);
    }

    public async Task<AdminChapterDto> Handle(ScheduleChapterCommand request, CancellationToken ct)
    {
        var chapter = await _db.Chapters.IgnoreQueryFilters().Include(x => x.Story).SingleOrDefaultAsync(x => x.Id == request.Id, ct) ?? throw Error("CHAPTER_NOT_FOUND", 404, "Chapter was not found.");
        if (chapter.DeletedAt is not null) throw Error("CHAPTER_DELETED", 400, "Deleted chapter cannot change publication state.");
        if (chapter.Story is null || chapter.Story.DeletedAt is not null) throw Error("STORY_DELETED", 400, "Chapter story is deleted.");
        
        if (chapter.Status != ChapterStatus.Draft)
        {
            throw Error("CHAPTER_NOT_PUBLISHABLE", 400, "Only draft chapters can be scheduled.");
        }
        
        CheckVersion(chapter.Version, request.Version);
        chapter.Schedule(request.ScheduledAt, _dateTime.UtcNow);

        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "CHAPTER_SCHEDULED",
            EntityType: "Chapter",
            EntityId: chapter.Id.ToString(),
            Result: "Success",
            Details: new { scheduledAt = request.ScheduledAt }
        ), ct);

        try
        {
            await Save(ct);
            await _cacheInvalidator.InvalidateStoryAndChaptersAsync(chapter.Story.Slug, ct);
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "CONCURRENCY_CONFLICT";
            await _auditWriter.WriteAsync(new AuditEvent(
                Action: "CHAPTER_SCHEDULED",
                EntityType: "Chapter",
                EntityId: chapter.Id.ToString(),
                Result: "Failed",
                Details: new { scheduledAt = request.ScheduledAt },
                ErrorCode: code
            ), ct);
            throw;
        }

        return ChapterDto(chapter);
    }

    private async Task<PublicationDto> ChangeStory(int id, int version, string action, CancellationToken ct)
    {
        var story = await _db.Stories.IgnoreQueryFilters().Include(x => x.Chapters).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("STORY_NOT_FOUND", 404, "Story was not found.");
        if (story.DeletedAt is not null) throw Error("STORY_DELETED", 400, "Deleted story cannot change publication state.");
        CheckVersion(story.Version, version);
        if (action == "publish" && !story.Chapters.Any(x => x.DeletedAt is null && x.Status == ChapterStatus.Published)) throw Error("STORY_REQUIRES_PUBLISHED_CHAPTER", 400, "Story requires a published chapter.");

        var oldStatus = story.Status.ToString();
        var auditAction = action switch
        {
            "publish" => "STORY_PUBLISHED",
            "unpublish" => "STORY_UNPUBLISHED",
            "hide" => "STORY_HIDDEN",
            _ => "STORY_COMPLETED"
        };
        var details = new { oldStatus, newStatus = action == "publish" ? "Published" : action == "unpublish" ? "Draft" : action == "hide" ? "Hidden" : "Completed" };

        try 
        { 
            if (action == "publish") story.Publish(_dateTime.UtcNow); 
            else if (action == "unpublish") story.Unpublish(_dateTime.UtcNow); 
            else if (action == "hide") story.Hide(_dateTime.UtcNow); 
            else story.Complete(_dateTime.UtcNow); 
        }
        catch (InvalidOperationException) 
        { 
            await _auditWriter.WriteAsync(new AuditEvent(auditAction, "Story", story.Id.ToString(), "Failed", details, "INVALID_STATUS_TRANSITION"), ct);
            throw Error("INVALID_STATUS_TRANSITION", 409, "Invalid story status transition."); 
        }

        await _auditWriter.WriteAsync(new AuditEvent(auditAction, "Story", story.Id.ToString(), "Success", details), ct);

        try
        {
            await Save(ct);
            await _cacheInvalidator.InvalidateStoryAndChaptersAsync(story.Slug, ct);
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "CONCURRENCY_CONFLICT";
            await _auditWriter.WriteAsync(new AuditEvent(auditAction, "Story", story.Id.ToString(), "Failed", details, code), ct);
            throw;
        }

        return new(story.Id, story.Status.ToString(), story.PublishedAt, story.Version);
    }

    private async Task<PublicationDto> ChangeChapter(int id, int version, string action, CancellationToken ct)
    {
        var chapter = await _db.Chapters.IgnoreQueryFilters().Include(x => x.Story).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("CHAPTER_NOT_FOUND", 404, "Chapter was not found.");
        if (chapter.DeletedAt is not null) throw Error("CHAPTER_DELETED", 400, "Deleted chapter cannot change publication state.");
        if (chapter.Story is null || chapter.Story.DeletedAt is not null) throw Error("STORY_DELETED", 400, "Chapter story is deleted.");
        CheckVersion(chapter.Version, version);

        var oldStatus = chapter.Status.ToString();
        var auditAction = action switch
        {
            "publish" => "CHAPTER_PUBLISHED",
            "unpublish" => "CHAPTER_UNPUBLISHED",
            _ => "CHAPTER_HIDDEN"
        };
        var details = new { oldStatus, newStatus = action == "publish" ? "Published" : action == "unpublish" ? "Draft" : "Hidden" };

        try 
        { 
            if (action == "publish") chapter.Publish(_dateTime.UtcNow); 
            else if (action == "unpublish") chapter.Unpublish(_dateTime.UtcNow); 
            else chapter.Hide(_dateTime.UtcNow); 
        }
        catch (InvalidOperationException) 
        { 
            var err = action == "publish" ? "CHAPTER_NOT_PUBLISHABLE" : "INVALID_STATUS_TRANSITION";
            await _auditWriter.WriteAsync(new AuditEvent(auditAction, "Chapter", chapter.Id.ToString(), "Failed", details, err), ct);
            throw Error(err, 400, "Invalid chapter publication request."); 
        }

        await _auditWriter.WriteAsync(new AuditEvent(auditAction, "Chapter", chapter.Id.ToString(), "Success", details), ct);

        try
        {
            await Save(ct);
            await _cacheInvalidator.InvalidateStoryAndChaptersAsync(chapter.Story.Slug, ct);
        }
        catch (System.Exception ex)
        {
            var code = ex is AppException ae ? ae.Code : "CONCURRENCY_CONFLICT";
            await _auditWriter.WriteAsync(new AuditEvent(auditAction, "Chapter", chapter.Id.ToString(), "Failed", details, code), ct);
            throw;
        }

        return new(chapter.Id, chapter.Status.ToString(), chapter.PublishedAt, chapter.Version);
    }

    private async Task Save(CancellationToken ct) { try { await _db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw Error("CONCURRENCY_CONFLICT", 409, "Record changed by another request."); } }
    private static void CheckVersion(int actual, int requested) { if (actual != requested) throw Error("CONCURRENCY_CONFLICT", 409, "Record changed by another request."); }
    private static AppException Error(string code, int status, string detail) => new(code, status, status == 404 ? "Not found" : status == 409 ? "Conflict" : "Validation failed", detail);

    private static AdminStoryDto StoryDto(Story story) => new(story.Id, story.Title, story.Slug, story.Description, story.CoverImageUrl, story.AuthorName, story.Genres.OrderBy(x => x.Name).Select(x => x.Name).ToList(), story.Status.ToString(), story.Version, story.PublishedAt, story.DeletedAt, story.CreateAt, story.UpdateAt, story.ScheduledAt);
    private static AdminChapterDto ChapterDto(Chapter chapter) => new(chapter.Id, chapter.StoryId, chapter.ChapterNumber, chapter.Title ?? string.Empty, chapter.Slug, chapter.Content ?? string.Empty, chapter.Status.ToString(), chapter.Version, chapter.PublishedAt, chapter.DeletedAt, chapter.CreateAt, chapter.UpdateAt, chapter.IsLocked, chapter.AffiliateLink, chapter.ScheduledAt);
}
