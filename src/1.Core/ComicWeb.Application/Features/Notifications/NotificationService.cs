using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Notifications;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _db;

    public NotificationService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task CreateNewChapterNotificationAsync(int storyId, string storyTitle, int chapterId, string chapterTitle, CancellationToken cancellationToken)
    {
        // 1. Get all user IDs following this story
        var followerIds = await _db.FollowedStories
            .Where(x => x.StoryId == storyId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        if (followerIds.Count == 0)
        {
            return;
        }

        // 2. Filter out users who already have a notification for this chapter (Deduplication)
        var existingNotificationUserIds = await _db.UserNotifications
            .Where(x => x.StoryId == storyId && x.ChapterId == chapterId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        var newFollowersToNotify = followerIds.Except(existingNotificationUserIds).ToList();
        if (newFollowersToNotify.Count == 0)
        {
            return;
        }

        // 3. Create and add notifications
        var notifications = newFollowersToNotify.Select(userId => new UserNotification
        {
            UserId = userId,
            StoryId = storyId,
            ChapterId = chapterId,
            Message = $"Truyện \"{storyTitle}\" vừa đăng chương mới: {chapterTitle}",
            IsRead = false
        }).ToList();

        await _db.UserNotifications.AddRangeAsync(notifications, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
