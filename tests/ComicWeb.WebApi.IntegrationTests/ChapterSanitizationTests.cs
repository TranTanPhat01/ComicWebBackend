using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public sealed class ChapterSanitizationTests(PostgreSqlFixture fixture) : IAsyncLifetime
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
    public async Task Create_chapter_sanitizes_html_and_rejects_empty_content()
    {
        var story = await AddStory("sanitize-create-story");

        using var client = await CreateAdminClient();

        // 1. Safe elements should remain, malicious elements should be sanitized
        var maliciousContent = "<p>Safe text here.</p><script>alert(1)</script><img src=\"https://example.com/a.jpg\" onerror=\"alert(1)\">";
        var createPayload = new
        {
            chapterNumber = 1,
            title = "Chapter One",
            slug = "chapter-one",
            content = maliciousContent
        };

        var response = await client.PostAsJsonAsync($"/api/v1/admin/stories/{story.Id}/chapters", createPayload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var responseDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var createdChapter = responseDoc.RootElement.GetProperty("data");
        var sanitizedContent = createdChapter.GetProperty("content").GetString();

        // Verify sanitization
        Assert.Contains("<p>Safe text here.</p>", sanitizedContent);
        Assert.Contains("src=\"https://example.com/a.jpg\"", sanitizedContent);
        Assert.DoesNotContain("script", sanitizedContent);
        Assert.DoesNotContain("onerror", sanitizedContent);

        // 2. An input that becomes completely empty/meaningless after sanitization should be rejected
        var uselessPayload = new
        {
            chapterNumber = 2,
            title = "Chapter Two",
            slug = "chapter-two",
            content = "<script>alert(1)</script><p><br></p>"
        };

        var uselessResponse = await client.PostAsJsonAsync($"/api/v1/admin/stories/{story.Id}/chapters", uselessPayload);
        Assert.Equal(HttpStatusCode.BadRequest, uselessResponse.StatusCode);
        Assert.Equal("CHAPTER_CONTENT_REQUIRED", await ProblemCode(uselessResponse));
    }

    [Fact]
    public async Task Update_chapter_sanitizes_html_and_rejects_empty_content()
    {
        var story = await AddStory("sanitize-update-story");
        var chapter = await AddChapter(story.Id, 1, "chapter-one");
        var version = await GetChapterVersion(chapter.Id);

        using var client = await CreateAdminClient();

        // 1. Safe update should sanitize malicious elements
        var maliciousContent = "<h4>New title</h4><iframe src=\"https://evil.com\"></iframe><a href=\"javascript:alert(1)\">Click</a><a href=\"https://google.com\">Google</a>";
        var updatePayload = new
        {
            chapterNumber = 1,
            title = "Chapter One Updated",
            slug = "chapter-one",
            content = maliciousContent,
            version = version
        };

        var response = await client.PutAsJsonAsync($"/api/v1/admin/stories/{story.Id}/chapters/{chapter.Id}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var responseDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var updatedChapter = responseDoc.RootElement.GetProperty("data");
        var sanitizedContent = updatedChapter.GetProperty("content").GetString();

        Assert.Contains("<h4>New title</h4>", sanitizedContent);
        Assert.Contains("<a href=\"https://google.com\">Google</a>", sanitizedContent);
        Assert.DoesNotContain("iframe", sanitizedContent);
        Assert.DoesNotContain("javascript", sanitizedContent);

        // 2. Rejecting empty updates
        var updatedVersion = updatedChapter.GetProperty("version").GetInt32();
        var uselessPayload = new
        {
            chapterNumber = 1,
            title = "Chapter One Empty",
            slug = "chapter-one",
            content = "<style>p {color:red}</style>    ",
            version = updatedVersion
        };

        var uselessResponse = await client.PutAsJsonAsync($"/api/v1/admin/stories/{story.Id}/chapters/{chapter.Id}", uselessPayload);
        Assert.Equal(HttpStatusCode.BadRequest, uselessResponse.StatusCode);
        Assert.Equal("CHAPTER_CONTENT_REQUIRED", await ProblemCode(uselessResponse));
    }

    [Fact]
    public async Task Public_reading_endpoint_returns_sanitized_content()
    {
        var story = await AddStory("public-sanitize-story");
        
        using var client = await CreateAdminClient();

        // Create with script and onerror
        var payload = new
        {
            chapterNumber = 1,
            title = "Chapter One",
            slug = "chapter-one",
            content = "<p>Real content</p><script>alert(1)</script><img src=\"https://example.com/b.jpg\" onerror=\"alert(1)\">"
        };
        var createResponse = await client.PostAsJsonAsync($"/api/v1/admin/stories/{story.Id}/chapters", payload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var doc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var chapterId = doc.RootElement.GetProperty("data").GetProperty("id").GetInt32();
        var chapterVersion = doc.RootElement.GetProperty("data").GetProperty("version").GetInt32();

        // Publish Chapter
        var pubChapter = await client.PostAsJsonAsync($"/api/v1/admin/chapters/{chapterId}/publish", new { version = chapterVersion });
        Assert.Equal(HttpStatusCode.OK, pubChapter.StatusCode);

        // Publish Story
        var storyVersion = await GetStoryVersion(story.Id);
        var pubStory = await client.PostAsJsonAsync($"/api/v1/admin/stories/{story.Id}/publish", new { version = storyVersion });
        Assert.Equal(HttpStatusCode.OK, pubStory.StatusCode);

        // Call public reading API
        using var publicClient = _factory!.CreateClient();
        var publicResponse = await publicClient.GetAsync($"/api/v1/stories/{story.Slug}/chapters/chapter-one");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);

        using var publicDoc = JsonDocument.Parse(await publicResponse.Content.ReadAsStringAsync());
        var publicContent = publicDoc.RootElement.GetProperty("data").GetProperty("content").GetString();

        Assert.Contains("<p>Real content</p>", publicContent);
        Assert.Contains("src=\"https://example.com/b.jpg\"", publicContent);
        Assert.DoesNotContain("script", publicContent);
        Assert.DoesNotContain("onerror", publicContent);
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

    private static async Task<string> ProblemCode(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString()!;
    }
}
