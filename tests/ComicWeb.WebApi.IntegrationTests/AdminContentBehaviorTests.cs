using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public sealed class AdminContentBehaviorTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private PostgreSqlApiFactory? _factory;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        _factory = new PostgreSqlApiFactory(fixture.ConnectionString);
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Restore_story_succeeds_and_repeat_restore_returns_stable_error()
    {
        var story = await AddStory("restore-story");
        await SoftDeleteStory(story.Id);
        var version = await GetStoryVersion(story.Id);

        using var client = await CreateAdminClient();
        var restored = await client.PostAsync(
            $"/api/v1/admin/stories/{story.Id}/restore?version={version}",
            content: null);

        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        await AssertStoryDeletedAt(story.Id, expectedDeleted: false);

        var repeat = await client.PostAsync(
            $"/api/v1/admin/stories/{story.Id}/restore?version={version + 1}",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, repeat.StatusCode);
        Assert.Equal("STORY_NOT_DELETED", await ProblemCode(repeat));
    }

    [Fact]
    public async Task Restore_story_returns_conflict_when_active_story_uses_its_slug()
    {
        var deleted = await AddStory("restore-conflict");
        await SoftDeleteStory(deleted.Id);
        var version = await GetStoryVersion(deleted.Id);
        await AddStory("restore-conflict");

        using var client = await CreateAdminClient();
        var response = await client.PostAsync(
            $"/api/v1/admin/stories/{deleted.Id}/restore?version={version}",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SLUG_CONFLICT", await ProblemCode(response));
    }

    [Fact]
    public async Task Restore_chapter_succeeds_and_conflict_is_reported()
    {
        var story = await AddStory("chapter-restore-parent");
        var deleted = await AddChapter(story.Id, 1, "deleted-chapter");
        await SoftDeleteChapter(deleted.Id);
        var version = await GetChapterVersion(deleted.Id);

        using var client = await CreateAdminClient();
        var restored = await client.PostAsync(
            $"/api/v1/admin/stories/{story.Id}/chapters/{deleted.Id}/restore?version={version}",
            content: null);

        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        await AssertChapterDeletedAt(deleted.Id, expectedDeleted: false);

        await SoftDeleteChapter(deleted.Id);
        version = await GetChapterVersion(deleted.Id);
        await AddChapter(story.Id, 1, "active-chapter");

        var conflict = await client.PostAsync(
            $"/api/v1/admin/stories/{story.Id}/chapters/{deleted.Id}/restore?version={version}",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("CHAPTER_CONFLICT", await ProblemCode(conflict));
    }

    [Fact]
    public async Task Story_and_chapter_stale_writes_return_concurrency_conflict()
    {
        var story = await AddStory("concurrent-story");
        var chapter = await AddChapter(story.Id, 1, "concurrent-chapter");
        var storyVersion = await GetStoryVersion(story.Id);
        var chapterVersion = await GetChapterVersion(chapter.Id);

        using var client = await CreateAdminClient();
        var storyUpdate = await client.PutAsJsonAsync(
            $"/api/v1/admin/stories/{story.Id}",
            StoryRequest("Updated story", "concurrent-story", storyVersion));
        var chapterUpdate = await client.PutAsJsonAsync(
            $"/api/v1/admin/stories/{story.Id}/chapters/{chapter.Id}",
            ChapterRequest(1, "Updated chapter", "concurrent-chapter", chapterVersion));

        Assert.Equal(HttpStatusCode.OK, storyUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, chapterUpdate.StatusCode);

        var staleStory = await client.PutAsJsonAsync(
            $"/api/v1/admin/stories/{story.Id}",
            StoryRequest("Stale story", "concurrent-story", storyVersion));
        var staleChapter = await client.PutAsJsonAsync(
            $"/api/v1/admin/stories/{story.Id}/chapters/{chapter.Id}",
            ChapterRequest(1, "Stale chapter", "concurrent-chapter", chapterVersion));

        Assert.Equal(HttpStatusCode.Conflict, staleStory.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", await ProblemCode(staleStory));
        Assert.Equal(HttpStatusCode.Conflict, staleChapter.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", await ProblemCode(staleChapter));
    }

    [Fact]
    public async Task Duplicate_chapter_number_returns_http_conflict_with_stable_code()
    {
        var story = await AddStory("duplicate-chapter-parent");
        await AddChapter(story.Id, 1, "existing-chapter");

        using var client = await CreateAdminClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/stories/{story.Id}/chapters",
            ChapterRequest(1, "Duplicate chapter", "duplicate-chapter", version: null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CHAPTER_NUMBER_CONFLICT", await ProblemCode(response));
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
        await using var database = fixture.CreateContext();
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
        await using var database = fixture.CreateContext();
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
        await using var database = fixture.CreateContext();
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

    private async Task SoftDeleteStory(int storyId)
    {
        await using var database = fixture.CreateContext();
        var story = await database.Stories.SingleAsync(item => item.Id == storyId);
        story.SoftDelete(DateTime.UtcNow);
        await database.SaveChangesAsync();
    }

    private async Task SoftDeleteChapter(int chapterId)
    {
        await using var database = fixture.CreateContext();
        var chapter = await database.Chapters.SingleAsync(item => item.Id == chapterId);
        chapter.SoftDelete(DateTime.UtcNow);
        await database.SaveChangesAsync();
    }

    private async Task<int> GetStoryVersion(int storyId)
    {
        await using var database = fixture.CreateContext();
        return await database.Stories
            .IgnoreQueryFilters()
            .Where(item => item.Id == storyId)
            .Select(item => item.Version)
            .SingleAsync();
    }

    private async Task<int> GetChapterVersion(int chapterId)
    {
        await using var database = fixture.CreateContext();
        return await database.Chapters
            .IgnoreQueryFilters()
            .Where(item => item.Id == chapterId)
            .Select(item => item.Version)
            .SingleAsync();
    }

    private async Task AssertStoryDeletedAt(int storyId, bool expectedDeleted)
    {
        await using var database = fixture.CreateContext();
        var deletedAt = await database.Stories
            .IgnoreQueryFilters()
            .Where(item => item.Id == storyId)
            .Select(item => item.DeletedAt)
            .SingleAsync();
        Assert.Equal(expectedDeleted, deletedAt is not null);
    }

    private async Task AssertChapterDeletedAt(int chapterId, bool expectedDeleted)
    {
        await using var database = fixture.CreateContext();
        var deletedAt = await database.Chapters
            .IgnoreQueryFilters()
            .Where(item => item.Id == chapterId)
            .Select(item => item.DeletedAt)
            .SingleAsync();
        Assert.Equal(expectedDeleted, deletedAt is not null);
    }

    private static object StoryRequest(string title, string slug, int version)
    {
        return new
        {
            title,
            slug,
            description = "Description",
            coverImageUrl = (string?)null,
            authorName = (string?)null,
            version
        };
    }

    private static object ChapterRequest(
        int chapterNumber,
        string title,
        string slug,
        int? version)
    {
        return new
        {
            chapterNumber,
            title,
            slug,
            content = "Content",
            version
        };
    }

    private static async Task<string> ProblemCode(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString()!;
    }
}
