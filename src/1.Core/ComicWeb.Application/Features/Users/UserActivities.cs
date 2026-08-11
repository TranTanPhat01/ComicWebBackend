using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Users;

// --- DTOs ---
public record FollowedStoryDto(
    int StoryId,
    string Title,
    string Slug,
    string? CoverUrl,
    string? AuthorName,
    string? GenreNames,
    DateTime FollowedAt);

public record ReadingHistoryDto(
    int StoryId,
    string StoryTitle,
    string StorySlug,
    string? CoverUrl,
    int ChapterId,
    int ChapterNumber,
    string? ChapterTitle,
    string ChapterSlug,
    DateTime LastReadAt);

public record HistoryMergeItem(int StoryId, int ChapterId, DateTime LastReadAt);

// --- Commands & Queries ---
public record FollowStoryCommand(int UserId, int StoryId) : IRequest;
public record UnfollowStoryCommand(int UserId, int StoryId) : IRequest;
public record GetFollowedStoriesQuery(int UserId, int Page = 1, int PageSize = 20) : IRequest<(IReadOnlyList<FollowedStoryDto> Items, int TotalCount)>;

public record UpsertReadingHistoryCommand(int UserId, int StoryId, int ChapterId) : IRequest;
public record DeleteReadingHistoryCommand(int UserId, int StoryId) : IRequest;
public record GetReadingHistoryQuery(int UserId, int Page = 1, int PageSize = 20) : IRequest<(IReadOnlyList<ReadingHistoryDto> Items, int TotalCount)>;

public record MergeUserActivitiesCommand(
    int UserId, 
    List<int> Follows, 
    List<HistoryMergeItem> Histories) : IRequest;

// --- Handlers ---
public class UserActivitiesHandler :
    IRequestHandler<FollowStoryCommand>,
    IRequestHandler<UnfollowStoryCommand>,
    IRequestHandler<GetFollowedStoriesQuery, (IReadOnlyList<FollowedStoryDto> Items, int TotalCount)>,
    IRequestHandler<UpsertReadingHistoryCommand>,
    IRequestHandler<DeleteReadingHistoryCommand>,
    IRequestHandler<GetReadingHistoryQuery, (IReadOnlyList<ReadingHistoryDto> Items, int TotalCount)>,
    IRequestHandler<MergeUserActivitiesCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UserActivitiesHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    // --- Follows Handler ---
    public async Task Handle(FollowStoryCommand request, CancellationToken ct)
    {
        var story = await _db.Stories.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StoryId, ct);
        if (story == null) throw new AppException("STORY_NOT_FOUND", 404, "Not found", "Story was not found.");

        var existing = await _db.FollowedStories
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && x.StoryId == request.StoryId, ct);

        if (existing == null)
        {
            var follow = new FollowedStory
            {
                UserId = request.UserId,
                StoryId = request.StoryId,
                CreatedAt = _clock.UtcNow
            };
            _db.FollowedStories.Add(follow);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task Handle(UnfollowStoryCommand request, CancellationToken ct)
    {
        var existing = await _db.FollowedStories
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && x.StoryId == request.StoryId, ct);

        if (existing != null)
        {
            _db.FollowedStories.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<(IReadOnlyList<FollowedStoryDto> Items, int TotalCount)> Handle(GetFollowedStoriesQuery request, CancellationToken ct)
    {
        var q = _db.FollowedStories.AsNoTracking()
            .Where(x => x.UserId == request.UserId);

        var total = await q.CountAsync(ct);

        var list = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new FollowedStoryDto(
                x.StoryId,
                x.Story.Title,
                x.Story.Slug,
                x.Story.CoverImageUrl,
                x.Story.AuthorName,
                string.Join(", ", x.Story.Genres.Select(g => g.Name)),
                x.CreatedAt
            ))
            .ToListAsync(ct);

        return (list, total);
    }

    // --- Reading History Handler ---
    public async Task Handle(UpsertReadingHistoryCommand request, CancellationToken ct)
    {
        var story = await _db.Stories.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StoryId, ct);
        if (story == null) throw new AppException("STORY_NOT_FOUND", 404, "Not found", "Story was not found.");

        var chapter = await _db.Chapters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ChapterId, ct);
        if (chapter == null) throw new AppException("CHAPTER_NOT_FOUND", 404, "Not found", "Chapter was not found.");

        var existing = await _db.ReadingHistories
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && x.StoryId == request.StoryId, ct);

        if (existing == null)
        {
            var history = new ReadingHistory
            {
                UserId = request.UserId,
                StoryId = request.StoryId,
                ChapterId = request.ChapterId,
                LastReadAt = _clock.UtcNow
            };
            _db.ReadingHistories.Add(history);
        }
        else
        {
            existing.ChapterId = request.ChapterId;
            existing.LastReadAt = _clock.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task Handle(DeleteReadingHistoryCommand request, CancellationToken ct)
    {
        var existing = await _db.ReadingHistories
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && x.StoryId == request.StoryId, ct);

        if (existing != null)
        {
            _db.ReadingHistories.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<(IReadOnlyList<ReadingHistoryDto> Items, int TotalCount)> Handle(GetReadingHistoryQuery request, CancellationToken ct)
    {
        var q = _db.ReadingHistories.AsNoTracking()
            .Where(x => x.UserId == request.UserId);

        var total = await q.CountAsync(ct);

        var list = await q
            .OrderByDescending(x => x.LastReadAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ReadingHistoryDto(
                x.StoryId,
                x.Story.Title,
                x.Story.Slug,
                x.Story.CoverImageUrl,
                x.ChapterId,
                x.Chapter.ChapterNumber,
                x.Chapter.Title,
                x.Chapter.Slug,
                x.LastReadAt
            ))
            .ToListAsync(ct);

        return (list, total);
    }

    // --- Merge Handler ---
    public async Task Handle(MergeUserActivitiesCommand request, CancellationToken ct)
    {
        // 1. Merge Follows
        if (request.Follows != null && request.Follows.Count > 0)
        {
            var existingFollows = await _db.FollowedStories
                .Where(x => x.UserId == request.UserId)
                .Select(x => x.StoryId)
                .ToListAsync(ct);

            var newFollows = request.Follows.Except(existingFollows).ToList();
            
            // Lọc ra các StoryId thực sự tồn tại trong DB
            var validStoryIds = await _db.Stories
                .Where(s => newFollows.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync(ct);

            foreach (var storyId in validStoryIds)
            {
                _db.FollowedStories.Add(new FollowedStory
                {
                    UserId = request.UserId,
                    StoryId = storyId,
                    CreatedAt = _clock.UtcNow
                });
            }
        }

        // 2. Merge Histories
        if (request.Histories != null && request.Histories.Count > 0)
        {
            var existingHistories = await _db.ReadingHistories
                .Where(x => x.UserId == request.UserId)
                .ToDictionaryAsync(x => x.StoryId, ct);

            foreach (var item in request.Histories)
            {
                // Kiểm tra sự tồn tại của Story và Chapter
                var storyExists = await _db.Stories.AnyAsync(s => s.Id == item.StoryId, ct);
                var chapterExists = await _db.Chapters.AnyAsync(c => c.Id == item.ChapterId, ct);
                if (!storyExists || !chapterExists) continue;

                if (existingHistories.TryGetValue(item.StoryId, out var existing))
                {
                    // Chỉ cập nhật nếu local history có thời gian đọc mới hơn
                    if (item.LastReadAt > existing.LastReadAt)
                    {
                        existing.ChapterId = item.ChapterId;
                        existing.LastReadAt = item.LastReadAt;
                    }
                }
                else
                {
                    _db.ReadingHistories.Add(new ReadingHistory
                    {
                        UserId = request.UserId,
                        StoryId = item.StoryId,
                        ChapterId = item.ChapterId,
                        LastReadAt = item.LastReadAt
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
