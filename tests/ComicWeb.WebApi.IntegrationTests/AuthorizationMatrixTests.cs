using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public sealed class AuthorizationMatrixTests(PostgreSqlFixture fixture) : IAsyncLifetime
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
    public async Task All_public_routes_remain_anonymous_for_missing_invalid_and_expired_tokens()
    {
        var story = await AddStory("public-auth", StoryStatus.Published);
        var chapter = await AddChapter(story.Id, "public-chapter", ChapterStatus.Published);
        var routes = new[]
        {
            "/api/v1/stories",
            "/api/v1/stories/public-auth",
            "/api/v1/stories/public-auth/chapters",
            $"/api/v1/stories/public-auth/chapters/{chapter.Slug}"
        };

        using var client = _factory!.CreateClient();
        var invalid = JwtTestTokenFactory.Create(1, UserRole.User, true, useInvalidSigningKey: true);
        var expired = JwtTestTokenFactory.Create(1, UserRole.User, true, DateTime.UtcNow.AddMinutes(-2));

        foreach (var route in routes)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await GetWithToken(client, route, invalid)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await GetWithToken(client, route, expired)).StatusCode);
        }

        // Verify that non-existent public slugs return 404 rather than 401/403 even with invalid/expired tokens
        var nonExistentRoutes = new[]
        {
            "/api/v1/stories/non-existent-story",
            "/api/v1/stories/public-auth/chapters/non-existent-chapter",
            "/api/v1/stories/non-existent-story/chapters"
        };
        foreach (var route in nonExistentRoutes)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await GetWithToken(client, route, invalid)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await GetWithToken(client, route, expired)).StatusCode);
        }
    }

    [Fact]
    public async Task Admin_story_route_enforces_the_complete_authorization_matrix()
    {
        var activeAdmin = await AddUser(UserRole.Admin, isActive: true);
        var inactiveAdmin = await AddUser(UserRole.Admin, isActive: false);
        var user = await AddUser(UserRole.User, isActive: true);
        const string route = "/api/v1/admin/stories";

        using var client = _factory!.CreateClient();
        await AssertProblem(await client.GetAsync(route), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(activeAdmin.Id, UserRole.Admin, true, useInvalidSigningKey: true)), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(activeAdmin.Id, UserRole.Admin, true, DateTime.UtcNow.AddMinutes(-2))), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(user.Id, UserRole.User, true)), HttpStatusCode.Forbidden, "FORBIDDEN");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(activeAdmin.Id, UserRole.Admin, false)), HttpStatusCode.Forbidden, "PASSWORD_CHANGE_REQUIRED");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(inactiveAdmin.Id, UserRole.Admin, true)), HttpStatusCode.Forbidden, "FORBIDDEN");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(999999, UserRole.Admin, true)), HttpStatusCode.Forbidden, "FORBIDDEN");

        Assert.Equal(HttpStatusCode.OK, (await GetWithToken(client, route, JwtTestTokenFactory.Create(activeAdmin.Id, UserRole.Admin, true))).StatusCode);
    }

    [Fact]
    public async Task Admin_chapter_route_enforces_the_complete_authorization_matrix()
    {
        var activeAdmin = await AddUser(UserRole.Admin, isActive: true);
        var inactiveAdmin = await AddUser(UserRole.Admin, isActive: false);
        var user = await AddUser(UserRole.User, isActive: true);
        const string route = "/api/v1/admin/stories/999999/chapters";

        using var client = _factory!.CreateClient();
        await AssertProblem(await client.GetAsync(route), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(activeAdmin.Id, UserRole.Admin, true, useInvalidSigningKey: true)), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(activeAdmin.Id, UserRole.Admin, true, DateTime.UtcNow.AddMinutes(-2))), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(user.Id, UserRole.User, true)), HttpStatusCode.Forbidden, "FORBIDDEN");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(activeAdmin.Id, UserRole.Admin, false)), HttpStatusCode.Forbidden, "PASSWORD_CHANGE_REQUIRED");
        await AssertProblem(await GetWithToken(client, route, JwtTestTokenFactory.Create(inactiveAdmin.Id, UserRole.Admin, true)), HttpStatusCode.Forbidden, "FORBIDDEN");

        var authorized = await GetWithToken(client, route, JwtTestTokenFactory.Create(activeAdmin.Id, UserRole.Admin, true));
        Assert.NotEqual(HttpStatusCode.Unauthorized, authorized.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, authorized.StatusCode);
    }

    private async Task<User> AddUser(UserRole role, bool isActive)
    {
        await using var database = fixture.CreateContext();
        var username = $"auth-{Guid.NewGuid():N}";
        var user = new User(
            username,
            $"{username}@example.test",
            BCrypt.Net.BCrypt.HashPassword("IntegrationAdmin@123"),
            role,
            mustChangePassword: false,
            DateTime.UtcNow);
        database.Users.Add(user);
        await database.SaveChangesAsync();

        if (!isActive)
        {
            await database.Database.ExecuteSqlRawAsync(
                "UPDATE \"Users\" SET \"IsActive\" = FALSE WHERE \"Id\" = {0}",
                user.Id);
        }

        return user;
    }

    private async Task<Story> AddStory(string slug, StoryStatus status)
    {
        await using var database = fixture.CreateContext();
        var story = new Story { Status = status };
        story.UpdateDetails(slug, slug, "Description", null, null, DateTime.UtcNow);
        database.Stories.Add(story);
        await database.SaveChangesAsync();
        return story;
    }

    private async Task<Chapter> AddChapter(int storyId, string slug, ChapterStatus status)
    {
        await using var database = fixture.CreateContext();
        var chapter = new Chapter { StoryId = storyId };
        chapter.UpdateContent(1, slug, slug, "Content", DateTime.UtcNow);
        if (status == ChapterStatus.Published)
        {
            chapter.Publish(DateTime.UtcNow);
        }

        database.Chapters.Add(chapter);
        await database.SaveChangesAsync();
        return chapter;
    }

    private static async Task<HttpResponseMessage> GetWithToken(HttpClient client, string route, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("requestId").GetString()));
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
    }
}
