using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public sealed class ScheduledPublishingTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private TestDateTimeProvider? _timeProvider;
    private ScheduledPublishingApiFactory? _factory;

    public ScheduledPublishingTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _timeProvider = new TestDateTimeProvider();
        _factory = new ScheduledPublishingApiFactory(_fixture.ConnectionString, _timeProvider);
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Schedule_endpoints_allow_scheduling_and_reject_non_drafts()
    {
        var story = await AddStory("schedule-endpoint-story");
        var chapter = await AddChapter(story.Id, 1, "schedule-endpoint-chapter");
        var storyVersion = await GetStoryVersion(story.Id);
        var chapterVersion = await GetChapterVersion(chapter.Id);

        using var client = await CreateAdminClient();

        // 1. Schedule Story
        var scheduleTime = _timeProvider!.UtcNow.AddMinutes(30);
        var storyResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/stories/{story.Id}/schedule",
            new { scheduledAt = scheduleTime, version = storyVersion });
        Assert.Equal(HttpStatusCode.OK, storyResponse.StatusCode);

        using var storyDoc = JsonDocument.Parse(await storyResponse.Content.ReadAsStringAsync());
        var returnedStory = storyDoc.RootElement.GetProperty("data");
        Assert.Equal(scheduleTime.ToString("o"), returnedStory.GetProperty("scheduledAt").GetDateTime().ToString("o"));

        // 2. Schedule Chapter
        var chapterResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/chapters/{chapter.Id}/schedule",
            new { scheduledAt = scheduleTime, version = chapterVersion });
        Assert.Equal(HttpStatusCode.OK, chapterResponse.StatusCode);

        using var chapterDoc = JsonDocument.Parse(await chapterResponse.Content.ReadAsStringAsync());
        var returnedChapter = chapterDoc.RootElement.GetProperty("data");
        Assert.Equal(scheduleTime.ToString("o"), returnedChapter.GetProperty("scheduledAt").GetDateTime().ToString("o"));

        // 3. Reject scheduling non-drafts (Publish first, then try to schedule)
        var updatedChapterVersion = returnedChapter.GetProperty("version").GetInt32();
        var publishResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/chapters/{chapter.Id}/publish",
            new { version = updatedChapterVersion });
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        using var pubDoc = JsonDocument.Parse(await publishResponse.Content.ReadAsStringAsync());
        var publishedVersion = pubDoc.RootElement.GetProperty("data").GetProperty("version").GetInt32();

        var invalidResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/chapters/{chapter.Id}/schedule",
            new { scheduledAt = scheduleTime, version = publishedVersion });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal("CHAPTER_NOT_PUBLISHABLE", await ProblemCode(invalidResponse));
    }

    [Fact]
    public async Task Auto_publishing_cycles_publish_due_content_correctly()
    {
        var story = await AddStory("due-publishing-story");
        var chapter = await AddChapter(story.Id, 1, "due-publishing-chapter");
        var storyVersion = await GetStoryVersion(story.Id);
        var chapterVersion = await GetChapterVersion(chapter.Id);

        using var client = await CreateAdminClient();

        // 1. Schedule both
        var scheduleTime = _timeProvider!.UtcNow.AddMinutes(10);
        await client.PostAsJsonAsync($"/api/v1/admin/stories/{story.Id}/schedule", new { scheduledAt = scheduleTime, version = storyVersion });
        await client.PostAsJsonAsync($"/api/v1/admin/chapters/{chapter.Id}/schedule", new { scheduledAt = scheduleTime, version = chapterVersion });

        // Run cycle now (time has not reached scheduleTime)
        var resultBefore = await RunPublishingCycleAsync();
        Assert.Equal(0, resultBefore.ChaptersPublished);
        Assert.Equal(0, resultBefore.StoriesPublished);

        // Advance time past scheduleTime
        _timeProvider.UtcNow = _timeProvider.UtcNow.AddMinutes(15);

        // Run cycle. Due chapters are processed first, then stories.
        // Wait, story requires at least one published chapter. Since the chapter is processed and published in this cycle, the story should publish in the same cycle!
        var resultAfter = await RunPublishingCycleAsync();
        Assert.Equal(1, resultAfter.ChaptersPublished);
        Assert.Equal(1, resultAfter.StoriesPublished);

        // Verify status in DB
        await using var db = _fixture.CreateContext();
        var chapterInDb = await db.Chapters.IgnoreQueryFilters().SingleAsync(x => x.Id == chapter.Id);
        Assert.Equal(ChapterStatus.Published, chapterInDb.Status);
        Assert.Null(chapterInDb.ScheduledAt);
        Assert.NotNull(chapterInDb.PublishedAt);

        var storyInDb = await db.Stories.IgnoreQueryFilters().SingleAsync(x => x.Id == story.Id);
        Assert.Equal(StoryStatus.Published, storyInDb.Status);
        Assert.Null(storyInDb.ScheduledAt);
        Assert.NotNull(storyInDb.PublishedAt);
    }

    [Fact]
    public async Task Story_without_published_chapter_is_skipped()
    {
        var story = await AddStory("skipped-story");
        var storyVersion = await GetStoryVersion(story.Id);

        using var client = await CreateAdminClient();

        // Schedule story
        var scheduleTime = _timeProvider!.UtcNow.AddMinutes(10);
        await client.PostAsJsonAsync($"/api/v1/admin/stories/{story.Id}/schedule", new { scheduledAt = scheduleTime, version = storyVersion });

        // Advance time
        _timeProvider.UtcNow = _timeProvider.UtcNow.AddMinutes(15);

        // Run cycle
        var result = await RunPublishingCycleAsync();
        Assert.Equal(0, result.StoriesPublished);
        Assert.Equal(1, result.StoriesFailed); // Failed because it does not have a published chapter

        // Story status should remain Draft
        await using var db = _fixture.CreateContext();
        var storyInDb = await db.Stories.SingleAsync(x => x.Id == story.Id);
        Assert.Equal(StoryStatus.Draft, storyInDb.Status);
        Assert.NotNull(storyInDb.ScheduledAt); // ScheduledAt is NOT cleared
    }

    [Fact]
    public async Task Deleted_and_hidden_contents_are_not_auto_published()
    {
        var story = await AddStory("deleted-check-story");
        var chapter = await AddChapter(story.Id, 1, "deleted-check-chapter");
        var storyVersion = await GetStoryVersion(story.Id);
        var chapterVersion = await GetChapterVersion(chapter.Id);

        using var client = await CreateAdminClient();

        // Schedule
        var scheduleTime = _timeProvider!.UtcNow.AddMinutes(10);
        await client.PostAsJsonAsync($"/api/v1/admin/stories/{story.Id}/schedule", new { scheduledAt = scheduleTime, version = storyVersion });
        await client.PostAsJsonAsync($"/api/v1/admin/chapters/{chapter.Id}/schedule", new { scheduledAt = scheduleTime, version = chapterVersion });

        // Soft delete chapter, hide story
        await SoftDeleteChapter(chapter.Id);
        await HideStory(story.Id);

        // Advance time
        _timeProvider.UtcNow = _timeProvider.UtcNow.AddMinutes(15);

        // Run cycle
        var result = await RunPublishingCycleAsync();
        Assert.Equal(0, result.ChaptersPublished);
        Assert.Equal(0, result.StoriesPublished);
    }

    [Fact]
    public async Task Publishing_is_idempotent()
    {
        var story = await AddStory("idempotency-story");
        var chapter = await AddChapter(story.Id, 1, "idempotency-chapter");
        var storyVersion = await GetStoryVersion(story.Id);
        var chapterVersion = await GetChapterVersion(chapter.Id);

        using var client = await CreateAdminClient();
        var scheduleTime = _timeProvider!.UtcNow.AddMinutes(10);
        await client.PostAsJsonAsync($"/api/v1/admin/stories/{story.Id}/schedule", new { scheduledAt = scheduleTime, version = storyVersion });
        await client.PostAsJsonAsync($"/api/v1/admin/chapters/{chapter.Id}/schedule", new { scheduledAt = scheduleTime, version = chapterVersion });

        _timeProvider.UtcNow = _timeProvider.UtcNow.AddMinutes(15);

        // Cycle 1: publishes both
        var res1 = await RunPublishingCycleAsync();
        Assert.Equal(1, res1.ChaptersPublished);
        Assert.Equal(1, res1.StoriesPublished);

        await using var db1 = _fixture.CreateContext();
        var firstPubChapter = await db1.Chapters.SingleAsync(x => x.Id == chapter.Id);
        var firstPubStory = await db1.Stories.SingleAsync(x => x.Id == story.Id);

        // Cycle 2: should do nothing (idempotent)
        var res2 = await RunPublishingCycleAsync();
        Assert.Equal(0, res2.ChaptersPublished);
        Assert.Equal(0, res2.StoriesPublished);

        await using var db2 = _fixture.CreateContext();
        var secondPubChapter = await db2.Chapters.SingleAsync(x => x.Id == chapter.Id);
        var secondPubStory = await db2.Stories.SingleAsync(x => x.Id == story.Id);

        // Properties must remain unchanged
        Assert.Equal(firstPubChapter.Version, secondPubChapter.Version);
        Assert.Equal(firstPubChapter.PublishedAt, secondPubChapter.PublishedAt);
        Assert.Equal(firstPubStory.Version, secondPubStory.Version);
        Assert.Equal(firstPubStory.PublishedAt, secondPubStory.PublishedAt);
    }

    [Fact]
    public async Task Batch_size_limits_processing_and_subsequent_runs_complete_it()
    {
        // Construct custom factory with BatchSize = 2
        using var smallBatchFactory = new ScheduledPublishingApiFactory(_fixture.ConnectionString, _timeProvider!, batchSize: 2);

        var story = await AddStory("batch-story");
        var c1 = await AddChapter(story.Id, 1, "c1");
        var c2 = await AddChapter(story.Id, 2, "c2");
        var c3 = await AddChapter(story.Id, 3, "c3");

        var c1Version = await GetChapterVersion(c1.Id);
        var c2Version = await GetChapterVersion(c2.Id);
        var c3Version = await GetChapterVersion(c3.Id);

        using var client = await CreateAdminClient();
        var scheduleTime = _timeProvider!.UtcNow.AddMinutes(10);
        await client.PostAsJsonAsync($"/api/v1/admin/chapters/{c1.Id}/schedule", new { scheduledAt = scheduleTime, version = c1Version });
        await client.PostAsJsonAsync($"/api/v1/admin/chapters/{c2.Id}/schedule", new { scheduledAt = scheduleTime, version = c2Version });
        await client.PostAsJsonAsync($"/api/v1/admin/chapters/{c3.Id}/schedule", new { scheduledAt = scheduleTime, version = c3Version });

        _timeProvider.UtcNow = _timeProvider.UtcNow.AddMinutes(15);

        // Run cycle 1 on smallBatchFactory: should only publish 2 chapters (limited by batchSize = 2)
        using var scope1 = smallBatchFactory.Services.CreateScope();
        var service1 = scope1.ServiceProvider.GetRequiredService<IScheduledPublishingService>();
        var result1 = await service1.PublishDueContentAsync();
        Assert.Equal(2, result1.ChaptersPublished);

        // Verify only 2 are published in DB
        await using (var db = _fixture.CreateContext())
        {
            var publishedCount = await db.Chapters.CountAsync(x => x.StoryId == story.Id && x.Status == ChapterStatus.Published);
            Assert.Equal(2, publishedCount);
        }

        // Run cycle 2: should publish the remaining 1 chapter
        using var scope2 = smallBatchFactory.Services.CreateScope();
        var service2 = scope2.ServiceProvider.GetRequiredService<IScheduledPublishingService>();
        var result2 = await service2.PublishDueContentAsync();
        Assert.Equal(1, result2.ChaptersPublished);

        // Verify all 3 are published now
        await using (var db = _fixture.CreateContext())
        {
            var publishedCount = await db.Chapters.CountAsync(x => x.StoryId == story.Id && x.Status == ChapterStatus.Published);
            Assert.Equal(3, publishedCount);
        }
    }

    [Fact]
    public async Task Optimistic_concurrency_failure_is_skipped_and_handled_gracefully()
    {
        using var concurrencyFactory = new ScheduledPublishingApiFactory(_fixture.ConnectionString, _timeProvider!, batchSize: 100, isConcurrencyTest: true);

        var story = await AddStory("concurrency-check-story");
        var chapter = await AddChapter(story.Id, 1, "concurrency-check-chapter");
        var storyVersion = await GetStoryVersion(story.Id);
        var chapterVersion = await GetChapterVersion(chapter.Id);

        using var client = await CreateAdminClient();
        var scheduleTime = _timeProvider!.UtcNow.AddMinutes(10);
        var scheduleResponse = await client.PostAsJsonAsync($"/api/v1/admin/chapters/{chapter.Id}/schedule", new { scheduledAt = scheduleTime, version = chapterVersion });
        Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);

        _timeProvider.UtcNow = _timeProvider.UtcNow.AddMinutes(15);

        // Run the cycle manually using the concurrencyFactory
        using var scope = concurrencyFactory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IScheduledPublishingService>();
        var result = await service.PublishDueContentAsync();

        Assert.Equal(0, result.ChaptersPublished);
        Assert.Equal(1, result.ChaptersSkipped); // Concurrency conflicts increment Skipped count
    }

    private async Task<ScheduledPublishingResult> RunPublishingCycleAsync()
    {
        using var scope = _factory!.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IScheduledPublishingService>();
        return await service.PublishDueContentAsync();
    }

    private async Task<HttpClient> CreateAdminClient()
    {
        await SeedAdministrator();
        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                usernameOrEmail = "integration-admin",
                password = "IntegrationAdmin@123"
            });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var document = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = document.RootElement
            .GetProperty("data")
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task SeedAdministrator()
    {
        await using var database = _fixture.CreateContext();
        if (await database.Users.AnyAsync(user => user.Username == "integration-admin"))
        {
            return;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationAdmin@123");
        var user = new User(
            "integration-admin",
            "integration-admin@example.test",
            passwordHash,
            UserRole.Admin,
            mustChangePassword: false,
            DateTime.UtcNow);

        database.Users.Add(user);
        await database.SaveChangesAsync();
    }

    private async Task<Story> AddStory(string slug)
    {
        await using var database = _fixture.CreateContext();
        var story = new Story();
        story.UpdateDetails(
            $"Story {slug}",
            slug,
            "Description",
            null,
            null,
            DateTime.UtcNow);
        database.Stories.Add(story);
        await database.SaveChangesAsync();
        return story;
    }

    private async Task<Chapter> AddChapter(int storyId, int chapterNumber, string slug)
    {
        await using var database = _fixture.CreateContext();
        var chapter = new Chapter
        {
            StoryId = storyId
        };
        chapter.UpdateContent(
            chapterNumber,
            $"Chapter {slug}",
            slug,
            "Content",
            DateTime.UtcNow);
        database.Chapters.Add(chapter);
        await database.SaveChangesAsync();
        return chapter;
    }

    private async Task SoftDeleteChapter(int chapterId)
    {
        await using var database = _fixture.CreateContext();
        var chapter = await database.Chapters.SingleAsync(item => item.Id == chapterId);
        chapter.SoftDelete(DateTime.UtcNow);
        await database.SaveChangesAsync();
    }

    private async Task HideStory(int storyId)
    {
        await using var database = _fixture.CreateContext();
        var story = await database.Stories.SingleAsync(item => item.Id == storyId);
        story.Publish(DateTime.UtcNow); // Publish first to allow hiding
        story.Hide(DateTime.UtcNow);
        await database.SaveChangesAsync();
    }

    private async Task<int> GetStoryVersion(int storyId)
    {
        await using var database = _fixture.CreateContext();
        return await database.Stories
            .IgnoreQueryFilters()
            .Where(item => item.Id == storyId)
            .Select(item => item.Version)
            .SingleAsync();
    }

    private async Task<int> GetChapterVersion(int chapterId)
    {
        await using var database = _fixture.CreateContext();
        return await database.Chapters
            .IgnoreQueryFilters()
            .Where(item => item.Id == chapterId)
            .Select(item => item.Version)
            .SingleAsync();
    }

    private static async Task<string> ProblemCode(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString()!;
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private sealed class ConcurrencyMockDbContext : IApplicationDbContext
    {
        private readonly IApplicationDbContext _inner;

        public ConcurrencyMockDbContext(IApplicationDbContext inner)
        {
            _inner = inner;
        }

        public DbSet<Story> Stories => _inner.Stories;
        public DbSet<Chapter> Chapters => _inner.Chapters;
        public DbSet<Genre> Genres => _inner.Genres;
        public DbSet<SystemLog> SystemLogs => _inner.SystemLogs;
        public DbSet<UserNotification> UserNotifications => _inner.UserNotifications;
        public DbSet<User> Users => _inner.Users;
        public DbSet<AuditLog> AuditLogs => _inner.AuditLogs;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            throw new DbUpdateConcurrencyException("Simulated concurrency exception");
        }
    }

    private sealed class ScheduledPublishingApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly TestDateTimeProvider _timeProvider;
        private readonly int _batchSize;
        private readonly bool _isConcurrencyTest;

        public ScheduledPublishingApiFactory(string connectionString, TestDateTimeProvider timeProvider, int batchSize = 100, bool isConcurrencyTest = false)
        {
            _connectionString = connectionString;
            _timeProvider = timeProvider;
            _batchSize = batchSize;
            _isConcurrencyTest = isConcurrencyTest;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
            builder.UseSetting("BootstrapAdmin:Enabled", "false");
            builder.UseSetting("DatabaseInitialization:ApplyMigrationsOnStartup", "false");
            builder.UseSetting("Jwt:Issuer", JwtTestTokenFactory.Issuer);
            builder.UseSetting("Jwt:Audience", JwtTestTokenFactory.Audience);
            builder.UseSetting("Jwt:SigningKey", "postgresql-test-signing-key-at-least-32-characters-long");

            // Disable background worker execution in integration tests to prevent race conditions
            builder.UseSetting("ScheduledPublishing:Enabled", "false");
            builder.UseSetting("ScheduledPublishing:BatchSize", _batchSize.ToString());

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDateTimeProvider));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<IDateTimeProvider>(_timeProvider);

                if (_isConcurrencyTest)
                {
                    var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IApplicationDbContext));
                    if (dbDescriptor != null)
                    {
                        services.Remove(dbDescriptor);
                    }
                    services.AddScoped<IApplicationDbContext>(provider =>
                    {
                        var realDb = provider.GetRequiredService<ComicWeb.Persistence.Contexts.ApplicationDbContext>();
                        return new ConcurrencyMockDbContext(realDb);
                    });
                }
            });
        }
    }
}
