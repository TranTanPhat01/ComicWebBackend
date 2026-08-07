using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Users;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ComicWeb.Application.Tests;

public class UserActivitiesTests
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

    private class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task FollowStory_ShouldCreateFollowRecord_WhenNotFollowedYet()
    {
        var db = new FakeApplicationDbContext();
        var now = DateTime.UtcNow;
        var story = new Story { Title = "Story Test", Slug = "story-test" };
        story.UpdateDetails("Story Test", "story-test", "desc", "cover", "author", now);
        db.Stories.Add(story);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UserActivitiesHandler(db, new FakeDateTimeProvider());

        await handler.Handle(new FollowStoryCommand(1, story.Id), CancellationToken.None);

        var follow = db.FollowedStories.FirstOrDefault(x => x.UserId == 1 && x.StoryId == story.Id);
        Assert.NotNull(follow);
    }

    [Fact]
    public async Task UnfollowStory_ShouldRemoveRecord_WhenFollowed()
    {
        var db = new FakeApplicationDbContext();
        var storyId = 100;
        db.FollowedStories.Add(new FollowedStory { UserId = 1, StoryId = storyId });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UserActivitiesHandler(db, new FakeDateTimeProvider());

        await handler.Handle(new UnfollowStoryCommand(1, storyId), CancellationToken.None);

        var follow = db.FollowedStories.FirstOrDefault(x => x.UserId == 1 && x.StoryId == storyId);
        Assert.Null(follow);
    }

    [Fact]
    public async Task UpsertReadingHistory_ShouldCreateOrUpdateRecord()
    {
        var db = new FakeApplicationDbContext();
        var now = DateTime.UtcNow;
        var story = new Story { Title = "Story Test", Slug = "story-test" };
        story.UpdateDetails("Story Test", "story-test", "desc", "cover", "author", now);
        db.Stories.Add(story);

        var chapter1 = new Chapter { StoryId = story.Id, Title = "Chapter 1", Slug = "chap-1" };
        var chapter2 = new Chapter { StoryId = story.Id, Title = "Chapter 2", Slug = "chap-2" };
        db.Chapters.Add(chapter1);
        db.Chapters.Add(chapter2);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UserActivitiesHandler(db, new FakeDateTimeProvider());

        // First read (Create)
        await handler.Handle(new UpsertReadingHistoryCommand(1, story.Id, chapter1.Id), CancellationToken.None);
        var record = db.ReadingHistories.FirstOrDefault(x => x.UserId == 1 && x.StoryId == story.Id);
        Assert.NotNull(record);
        Assert.Equal(chapter1.Id, record.ChapterId);

        // Second read (Update)
        await handler.Handle(new UpsertReadingHistoryCommand(1, story.Id, chapter2.Id), CancellationToken.None);
        record = db.ReadingHistories.FirstOrDefault(x => x.UserId == 1 && x.StoryId == story.Id);
        Assert.NotNull(record);
        Assert.Equal(chapter2.Id, record.ChapterId);
    }
}
