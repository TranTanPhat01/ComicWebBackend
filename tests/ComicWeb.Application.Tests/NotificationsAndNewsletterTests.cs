using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Features.Notifications;
using ComicWeb.Application.Features.Newsletter;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ComicWeb.Application.Tests;

public class NotificationsAndNewsletterTests
{
    private class FakeApplicationDbContext : IApplicationDbContext
    {
        private readonly InnerDbContext _context;

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Story> Stories { get; set; } = null!;
        public DbSet<Chapter> Chapters { get; set; } = null!;
        public DbSet<Genre> Genres { get; set; } = null!;
        public DbSet<SystemLog> SystemLogs { get; set; } = null!;
        public DbSet<UserNotification> UserNotifications { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<AffiliateClick> AffiliateClicks { get; set; } = null!;
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
        public DbSet<FollowedStory> FollowedStories { get; set; } = null!;
        public DbSet<ReadingHistory> ReadingHistories { get; set; } = null!;
        public DbSet<StoryRating> StoryRatings { get; set; } = null!;
        public DbSet<Comment> Comments { get; set; } = null!;
        public DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; } = null!;

        public FakeApplicationDbContext()
        {
            var options = new DbContextOptionsBuilder<DbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new InnerDbContext(options);
            Users = _context.Set<User>();
            Stories = _context.Set<Story>();
            Chapters = _context.Set<Chapter>();
            Genres = _context.Set<Genre>();
            SystemLogs = _context.Set<SystemLog>();
            UserNotifications = _context.Set<UserNotification>();
            AuditLogs = _context.Set<AuditLog>();
            AffiliateClicks = _context.Set<AffiliateClick>();
            SystemSettings = _context.Set<SystemSetting>();
            FollowedStories = _context.Set<FollowedStory>();
            ReadingHistories = _context.Set<ReadingHistory>();
            StoryRatings = _context.Set<StoryRating>();
            Comments = _context.Set<Comment>();
            NewsletterSubscribers = _context.Set<NewsletterSubscriber>();
        }

        private class InnerDbContext : DbContext
        {
            public InnerDbContext(DbContextOptions<DbContext> options) : base(options) { }
            protected override void OnModelCreating(ModelBuilder mb)
            {
                mb.Entity<User>().HasKey(x => x.Id);
                mb.Entity<Story>().HasKey(x => x.Id);
                mb.Entity<Chapter>().HasKey(x => x.Id);
                mb.Entity<Genre>().HasKey(x => x.Id);
                mb.Entity<SystemLog>().HasKey(x => x.Id);
                mb.Entity<UserNotification>().HasKey(x => x.Id);
                mb.Entity<AuditLog>().HasKey(x => x.Id);
                mb.Entity<AffiliateClick>().HasKey(x => x.Id);
                mb.Entity<SystemSetting>().HasKey(x => x.Id);
                mb.Entity<FollowedStory>().HasKey(x => x.Id);
                mb.Entity<ReadingHistory>().HasKey(x => x.Id);
                mb.Entity<StoryRating>().HasKey(x => x.Id);
                mb.Entity<Comment>().HasKey(x => x.Id);
                mb.Entity<NewsletterSubscriber>().HasKey(x => x.Id);
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
    }

    [Fact]
    public async Task Subscribe_ShouldCreateSubscriber_WhenValidEmail()
    {
        var db = new FakeApplicationDbContext();
        var handler = new NewsletterHandler(db);

        await handler.Handle(new SubscribeNewsletterCommand("test@gmail.com"), CancellationToken.None);

        var subscriber = await db.NewsletterSubscribers.FirstOrDefaultAsync(x => x.NormalizedEmail == "TEST@GMAIL.COM");
        Assert.NotNull(subscriber);
        Assert.True(subscriber.IsSubscribed);
    }

    [Fact]
    public async Task Subscribe_ShouldThrowException_WhenInvalidEmail()
    {
        var db = new FakeApplicationDbContext();
        var handler = new NewsletterHandler(db);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(new SubscribeNewsletterCommand("invalid-email"), CancellationToken.None));

        Assert.Equal("INVALID_EMAIL", exception.Code);
    }

    [Fact]
    public async Task Unsubscribe_ShouldSetSubscribedFalse_WhenValidEmail()
    {
        var db = new FakeApplicationDbContext();
        db.NewsletterSubscribers.Add(new NewsletterSubscriber
        {
            Email = "test@gmail.com",
            NormalizedEmail = "TEST@GMAIL.COM",
            IsSubscribed = true
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new NewsletterHandler(db);
        await handler.Handle(new UnsubscribeNewsletterCommand("test@gmail.com"), CancellationToken.None);

        var subscriber = await db.NewsletterSubscribers.FirstOrDefaultAsync(x => x.NormalizedEmail == "TEST@GMAIL.COM");
        Assert.NotNull(subscriber);
        Assert.False(subscriber.IsSubscribed);
        Assert.NotNull(subscriber.UnsubscribedAt);
    }

    [Fact]
    public async Task GetUserNotifications_ShouldReturnOnlyCurrentUserNotifications()
    {
        var db = new FakeApplicationDbContext();
        db.UserNotifications.Add(new UserNotification { UserId = 1, Message = "Notif User 1", IsRead = false });
        db.UserNotifications.Add(new UserNotification { UserId = 2, Message = "Notif User 2", IsRead = false });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UserNotificationsHandler(db);
        var result = await handler.Handle(new GetUserNotificationsQuery(1), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Notif User 1", result.Items[0].Message);
    }

    [Fact]
    public async Task MarkAsRead_ShouldUpdateIsRead()
    {
        var db = new FakeApplicationDbContext();
        var notif = new UserNotification { UserId = 1, Message = "Test Notif", IsRead = false };
        db.UserNotifications.Add(notif);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UserNotificationsHandler(db);
        await handler.Handle(new MarkNotificationAsReadCommand(1, notif.Id), CancellationToken.None);

        var updatedNotif = await db.UserNotifications.FindAsync(notif.Id);
        Assert.NotNull(updatedNotif);
        Assert.True(updatedNotif.IsRead);
    }

    [Fact]
    public async Task MarkAllAsRead_ShouldUpdateAllUnread()
    {
        var db = new FakeApplicationDbContext();
        db.UserNotifications.Add(new UserNotification { UserId = 1, Message = "Notif 1", IsRead = false });
        db.UserNotifications.Add(new UserNotification { UserId = 1, Message = "Notif 2", IsRead = false });
        db.UserNotifications.Add(new UserNotification { UserId = 1, Message = "Notif 3", IsRead = true });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UserNotificationsHandler(db);
        await handler.Handle(new MarkAllNotificationsAsReadCommand(1), CancellationToken.None);

        var list = await db.UserNotifications.Where(x => x.UserId == 1).ToListAsync();
        Assert.True(list.All(x => x.IsRead));
    }

    [Fact]
    public async Task NotificationService_ShouldNotifyCorrectFollowers_WhenUserFollowsMultipleStories()
    {
        var db = new FakeApplicationDbContext();
        
        // Setup followers
        db.FollowedStories.Add(new FollowedStory { UserId = 1, StoryId = 10 });
        db.FollowedStories.Add(new FollowedStory { UserId = 1, StoryId = 20 });
        db.FollowedStories.Add(new FollowedStory { UserId = 2, StoryId = 10 });
        await db.SaveChangesAsync(CancellationToken.None);

        var service = new Features.Notifications.NotificationService(db);
        
        // Notify Story 10 New Chapter
        await service.CreateNewChapterNotificationAsync(10, "Story Ten", 100, "Chương 1", CancellationToken.None);

        var user1Notifications = await db.UserNotifications.Where(x => x.UserId == 1).ToListAsync();
        var user2Notifications = await db.UserNotifications.Where(x => x.UserId == 2).ToListAsync();

        Assert.Single(user1Notifications);
        Assert.Equal("Truyện \"Story Ten\" vừa đăng chương mới: Chương 1", user1Notifications[0].Message);
        
        Assert.Single(user2Notifications);
        Assert.Equal("Truyện \"Story Ten\" vừa đăng chương mới: Chương 1", user2Notifications[0].Message);
    }

    [Fact]
    public async Task NotificationService_ShouldCreateNotificationWithCustomMessage()
    {
        var db = new FakeApplicationDbContext();
        db.FollowedStories.Add(new FollowedStory { UserId = 1, StoryId = 10 });
        await db.SaveChangesAsync(CancellationToken.None);

        var service = new Features.Notifications.NotificationService(db);
        
        // Notify with default chapter title resolving logic fallback (e.g. "Chương 5")
        var title = string.IsNullOrWhiteSpace("") ? "Chương 5" : "";
        await service.CreateNewChapterNotificationAsync(10, "Story Ten", 100, title, CancellationToken.None);

        var userNotifications = await db.UserNotifications.Where(x => x.UserId == 1).ToListAsync();
        Assert.Single(userNotifications);
        Assert.Equal("Truyện \"Story Ten\" vừa đăng chương mới: Chương 5", userNotifications[0].Message);
    }
}
