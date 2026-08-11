using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Stories;

public sealed class ScheduledPublishingService : IScheduledPublishingService
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<ScheduledPublishingService> _logger;
    private readonly ScheduledPublishingOptions _options;
    private readonly IAuditContextAccessor _auditContextAccessor;
    private readonly IAuditWriter _auditWriter;
    private readonly IPublicContentCacheInvalidator _cacheInvalidator;

    private readonly INotificationService _notificationService;

    public ScheduledPublishingService(
        IApplicationDbContext db,
        IDateTimeProvider dateTime,
        IOptions<ScheduledPublishingOptions> options,
        ILogger<ScheduledPublishingService> logger,
        IAuditContextAccessor auditContextAccessor,
        IAuditWriter auditWriter,
        IPublicContentCacheInvalidator cacheInvalidator,
        INotificationService notificationService)
    {
        _db = db;
        _dateTime = dateTime;
        _logger = logger;
        _options = options.Value;
        _auditContextAccessor = auditContextAccessor;
        _auditWriter = auditWriter;
        _cacheInvalidator = cacheInvalidator;
        _notificationService = notificationService;
    }

    public async Task<ScheduledPublishingResult> PublishDueContentAsync(CancellationToken cancellationToken = default)
    {
        using var systemScope = _auditContextAccessor.UseSystemContext();

        var result = new ScheduledPublishingResult();
        var now = _dateTime.UtcNow;
        var batchSize = _options.BatchSize;

        _logger.LogDebug("Scheduled publishing run started at {Now} UTC (BatchSize: {BatchSize})", now, batchSize);

        // 1. Process due Chapters first
        var dueChapters = await _db.Chapters
            .Include(x => x.Story)
            .Where(x => x.ScheduledAt != null && x.ScheduledAt <= now && x.Status == ChapterStatus.Draft)
            .OrderBy(x => x.ScheduledAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var chapter in dueChapters)
        {
            result.ChaptersScanned++;

            if (chapter.Story == null || chapter.Story.DeletedAt != null)
            {
                _logger.LogWarning("Skipping auto-publish for Chapter {ChapterId}: Parent story does not exist or is soft-deleted.", chapter.Id);
                result.ChaptersFailed++;
                continue;
            }

            try
            {
                var scheduledAt = chapter.ScheduledAt;
                chapter.Publish(now);

                await _auditWriter.WriteAsync(new AuditEvent(
                    Action: "SCHEDULED_CHAPTER_PUBLISHED",
                    EntityType: "Chapter",
                    EntityId: chapter.Id.ToString(),
                    Result: "Success",
                    Details: new { scheduledAt, publishedAt = now }
                ), cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
                await _cacheInvalidator.InvalidateStoryAndChaptersAsync(chapter.Story.Slug, cancellationToken);
                
                var title = string.IsNullOrWhiteSpace(chapter.Title) ? $"Chương {chapter.ChapterNumber}" : chapter.Title.Trim();
                await _notificationService.CreateNewChapterNotificationAsync(chapter.StoryId, chapter.Story.Title, chapter.Id, title, cancellationToken);

                result.ChaptersPublished++;
                _logger.LogInformation("Successfully auto-published Chapter {ChapterId} for Story {StoryId}.", chapter.Id, chapter.StoryId);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogInformation("Concurrency conflict when auto-publishing Chapter {ChapterId}. Record was likely modified by another process.", chapter.Id);
                result.ChaptersSkipped++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error auto-publishing Chapter {ChapterId}.", chapter.Id);
                result.ChaptersFailed++;
            }
        }

        // 2. Process due Stories
        var dueStories = await _db.Stories
            .Include(x => x.Chapters)
            .Where(x => x.ScheduledAt != null && x.ScheduledAt <= now && x.Status == StoryStatus.Draft)
            .OrderBy(x => x.ScheduledAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var story in dueStories)
        {
            result.StoriesScanned++;

            // Validation: Story requires at least one published chapter
            var hasPublishedChapter = story.Chapters.Any(x => x.DeletedAt == null && x.Status == ChapterStatus.Published);
            if (!hasPublishedChapter)
            {
                _logger.LogWarning("Skipping auto-publish for Story {StoryId}: Story must have at least one Published chapter.", story.Id);
                result.StoriesFailed++;
                continue;
            }

            try
            {
                var scheduledAt = story.ScheduledAt;
                story.Publish(now);

                await _auditWriter.WriteAsync(new AuditEvent(
                    Action: "SCHEDULED_STORY_PUBLISHED",
                    EntityType: "Story",
                    EntityId: story.Id.ToString(),
                    Result: "Success",
                    Details: new { scheduledAt, publishedAt = now }
                ), cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
                await _cacheInvalidator.InvalidateStoryAndChaptersAsync(story.Slug, cancellationToken);
                result.StoriesPublished++;
                _logger.LogInformation("Successfully auto-published Story {StoryId} (Slug: {Slug}).", story.Id, story.Slug);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogInformation("Concurrency conflict when auto-publishing Story {StoryId}. Record was likely modified by another process.", story.Id);
                result.StoriesSkipped++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error auto-publishing Story {StoryId}.", story.Id);
                result.StoriesFailed++;
            }
        }

        if (result.ChaptersPublished > 0 || result.StoriesPublished > 0)
        {
            _logger.LogInformation("Scheduled publishing run completed: Published {ChaptersCount} chapters and {StoriesCount} stories.", result.ChaptersPublished, result.StoriesPublished);
        }

        return result;
    }
}
