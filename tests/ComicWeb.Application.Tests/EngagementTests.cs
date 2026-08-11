using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Stories;
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

public class EngagementTests
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
    public async Task RateStory_ShouldCreateRating_WhenFirstTime()
    {
        var db = new FakeApplicationDbContext();
        var now = DateTime.UtcNow;
        var story = new Story { Title = "Story Test", Slug = "story-test" };
        story.UpdateDetails("Story Test", "story-test", "desc", "cover", "author", now);
        db.Stories.Add(story);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RateStoryCommandHandler(db, new FakeDateTimeProvider());

        var result = await handler.Handle(new RateStoryCommand(1, story.Id, 5), CancellationToken.None);

        Assert.Equal(5, result.Score);
        var rating = db.StoryRatings.FirstOrDefault(r => r.UserId == 1 && r.StoryId == story.Id);
        Assert.NotNull(rating);
        Assert.Equal(5, rating.Score);
    }

    [Fact]
    public async Task RateStory_ShouldUpdateRating_WhenAlreadyRated()
    {
        var db = new FakeApplicationDbContext();
        var now = DateTime.UtcNow;
        var story = new Story { Title = "Story Test", Slug = "story-test" };
        story.UpdateDetails("Story Test", "story-test", "desc", "cover", "author", now);
        db.Stories.Add(story);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RateStoryCommandHandler(db, new FakeDateTimeProvider());

        // First rating
        await handler.Handle(new RateStoryCommand(1, story.Id, 4), CancellationToken.None);

        // Update rating
        var result = await handler.Handle(new RateStoryCommand(1, story.Id, 2), CancellationToken.None);

        Assert.Equal(2, result.Score);
        var rating = db.StoryRatings.FirstOrDefault(r => r.UserId == 1 && r.StoryId == story.Id);
        Assert.NotNull(rating);
        Assert.Equal(2, rating.Score);
    }

    [Fact]
    public async Task CreateComment_ShouldSucceed_WhenValidContent()
    {
        var db = new FakeApplicationDbContext();
        var now = DateTime.UtcNow;
        var story = new Story { Title = "Story Test", Slug = "story-test" };
        story.UpdateDetails("Story Test", "story-test", "desc", "cover", "author", now);
        db.Stories.Add(story);

        var user = new User("tester", "tester@test.com", "hash", UserRole.User, false, now);
        db.Users.Add(user);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateCommentCommandHandler(db, new FakeDateTimeProvider());

        var result = await handler.Handle(new CreateCommentCommand(user.Id, story.Id, null, null, "Great story!"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Great story!", result.Content);
        Assert.Equal("tester", result.Author.Username);
        
        var comment = db.Comments.FirstOrDefault(c => c.Id == result.Id);
        Assert.NotNull(comment);
        Assert.Equal(CommentStatus.Active, comment.Status);
    }

    [Fact]
    public async Task CreateComment_ShouldFail_WhenReplyToReply()
    {
        var db = new FakeApplicationDbContext();
        var now = DateTime.UtcNow;
        var story = new Story { Title = "Story Test", Slug = "story-test" };
        story.UpdateDetails("Story Test", "story-test", "desc", "cover", "author", now);
        db.Stories.Add(story);

        var user = new User("tester", "tester@test.com", "hash", UserRole.User, false, now);
        db.Users.Add(user);
        await db.SaveChangesAsync(CancellationToken.None);

        // Top-level comment
        var comment1 = new Comment { StoryId = story.Id, UserId = user.Id, Content = "Top comment", Status = CommentStatus.Active };
        db.Comments.Add(comment1);
        await db.SaveChangesAsync(CancellationToken.None);

        // Level 2 comment (reply to top)
        var comment2 = new Comment { StoryId = story.Id, UserId = user.Id, ParentCommentId = comment1.Id, Content = "Level 2 comment", Status = CommentStatus.Active };
        db.Comments.Add(comment2);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateCommentCommandHandler(db, new FakeDateTimeProvider());

        // Try reply to Level 2 (Should Fail with depth restriction)
        var exception = await Assert.ThrowsAsync<AppException>(() => 
            handler.Handle(new CreateCommentCommand(user.Id, story.Id, null, comment2.Id, "Level 3 attempt"), CancellationToken.None)
        );

        Assert.Equal("NESTED_LIMIT_EXCEEDED", exception.Code);
    }

    [Fact]
    public async Task DeleteComment_ShouldSoftDelete_WhenOwner()
    {
        var db = new FakeApplicationDbContext();
        var now = DateTime.UtcNow;
        var story = new Story { Title = "Story Test", Slug = "story-test" };
        story.UpdateDetails("Story Test", "story-test", "desc", "cover", "author", now);
        db.Stories.Add(story);

        var user = new User("tester", "tester@test.com", "hash", UserRole.User, false, now);
        db.Users.Add(user);
        await db.SaveChangesAsync(CancellationToken.None);

        var comment = new Comment { StoryId = story.Id, UserId = user.Id, Content = "Delete me", Status = CommentStatus.Active };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteCommentCommandHandler(db, new FakeDateTimeProvider());

        await handler.Handle(new DeleteCommentCommand(user.Id, comment.Id, false), CancellationToken.None);

        var dbComment = db.Comments.FirstOrDefault(c => c.Id == comment.Id);
        Assert.NotNull(dbComment);
        Assert.NotNull(dbComment.DeletedAt);
    }

    [Fact]
    public async Task DeleteComment_ShouldThrowException_WhenNotOwnerOrAdmin()
    {
        var db = new FakeApplicationDbContext();
        var now = DateTime.UtcNow;
        var story = new Story { Title = "Story Test", Slug = "story-test" };
        story.UpdateDetails("Story Test", "story-test", "desc", "cover", "author", now);
        db.Stories.Add(story);

        var owner = new User("owner", "owner@test.com", "hash", UserRole.User, false, now) { Id = 1 };
        var otherUser = new User("other", "other@test.com", "hash", UserRole.User, false, now) { Id = 2 };
        db.Users.Add(owner);
        db.Users.Add(otherUser);
        await db.SaveChangesAsync(CancellationToken.None);

        var comment = new Comment { StoryId = story.Id, UserId = owner.Id, Content = "Secret Comment", Status = CommentStatus.Active };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteCommentCommandHandler(db, new FakeDateTimeProvider());

        // Try deleting another user's comment (should throw)
        var exception = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(new DeleteCommentCommand(otherUser.Id, comment.Id, false), CancellationToken.None)
        );

        Assert.Equal("UNAUTHORIZED_ACTION", exception.Code);

        // Verify comment is NOT deleted
        var dbComment = db.Comments.FirstOrDefault(c => c.Id == comment.Id);
        Assert.NotNull(dbComment);
        Assert.Null(dbComment.DeletedAt);
    }
}
