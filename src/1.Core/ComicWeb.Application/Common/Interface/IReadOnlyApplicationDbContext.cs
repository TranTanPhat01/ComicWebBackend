using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Interface;

public interface IReadOnlyApplicationDbContext
{
    DbSet<Story> Stories { get; }
    DbSet<Chapter> Chapters { get; }
    DbSet<Genre> Genres { get; }
    DbSet<SystemLog> SystemLogs { get; }
    DbSet<UserNotification> UserNotifications { get; }
    DbSet<User> Users { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AffiliateClick> AffiliateClicks { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<FollowedStory> FollowedStories { get; }
    DbSet<ReadingHistory> ReadingHistories { get; }
    DbSet<StoryRating> StoryRatings { get; }
    DbSet<Comment> Comments { get; }
    DbSet<NewsletterSubscriber> NewsletterSubscribers { get; }
}
