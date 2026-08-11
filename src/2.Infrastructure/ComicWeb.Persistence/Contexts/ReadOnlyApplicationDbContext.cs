using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Persistence.Contexts;

public sealed class ReadOnlyApplicationDbContext : DbContext, IReadOnlyApplicationDbContext
{
    public ReadOnlyApplicationDbContext(DbContextOptions<ReadOnlyApplicationDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<Story> Stories => Set<Story>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AffiliateClick> AffiliateClicks => Set<AffiliateClick>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<FollowedStory> FollowedStories => Set<FollowedStory>();
    public DbSet<ReadingHistory> ReadingHistories => Set<ReadingHistory>();
    public DbSet<StoryRating> StoryRatings => Set<StoryRating>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
