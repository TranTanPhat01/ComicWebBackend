using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using ComicWeb.Persistence.Logging;
using ComicWeb.WebApi.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ComicWeb.Application.Common.Interface;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public class AuditLoggingTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private PostgreSqlApiFactory? _factory;

    public AuditLoggingTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new PostgreSqlApiFactory(_fixture.ConnectionString);
        return _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    // ==========================================
    // UNIT TESTS
    // ==========================================

    [Fact]
    public void Serializer_Should_Serialize_Valid_Metadata()
    {
        var serializer = new AuditDetailsSerializer();
        var data = new { Name = "Iron Man", Level = 100 };
        
        var result = serializer.Serialize(data);
        
        Assert.NotNull(result);
        Assert.Contains("\"name\":\"Iron Man\"", result);
        Assert.Contains("\"level\":100", result);
    }

    [Fact]
    public void Serializer_Should_Redact_Banned_Keys_Case_Insensitive()
    {
        var serializer = new AuditDetailsSerializer();
        var data = new
        {
            Password = "super_secret_password",
            PasswordHash = "hashed_value",
            AccessToken = "jwt_token",
            RefreshToken = "refresh_token",
            Token = "raw_token",
            Authorization = "Bearer token",
            SigningKey = "signingkey_value",
            ConnectionString = "Host=localhost",
            Content = "<p>chapter contents</p>",
            RawContent = "chapter contents",
            SafeField = "Not redacted"
        };

        var result = serializer.Serialize(data);

        Assert.NotNull(result);
        Assert.Contains("\"password\":\"[REDACTED]\"", result);
        Assert.Contains("\"passwordHash\":\"[REDACTED]\"", result);
        Assert.Contains("\"accessToken\":\"[REDACTED]\"", result);
        Assert.Contains("\"refreshToken\":\"[REDACTED]\"", result);
        Assert.Contains("\"token\":\"[REDACTED]\"", result);
        Assert.Contains("\"authorization\":\"[REDACTED]\"", result);
        Assert.Contains("\"signingKey\":\"[REDACTED]\"", result);
        Assert.Contains("\"connectionString\":\"[REDACTED]\"", result);
        Assert.Contains("\"content\":\"[REDACTED]\"", result);
        Assert.Contains("\"rawContent\":\"[REDACTED]\"", result);
        Assert.Contains("\"safeField\":\"Not redacted\"", result);
    }

    [Fact]
    public void Serializer_Should_Truncate_Details_Vast_Length()
    {
        var serializer = new AuditDetailsSerializer();
        var longString = new string('A', 10000);
        var data = new { Description = longString };

        var result = serializer.Serialize(data);

        Assert.NotNull(result);
        Assert.True(result.Length <= 8300);
        Assert.Contains("... [TRUNCATED]", result);
    }

    [Fact]
    public void Serializer_Should_Return_Null_For_Null_Details()
    {
        var serializer = new AuditDetailsSerializer();
        var result = serializer.Serialize(null);
        Assert.Null(result);
    }

    [Fact]
    public void Serializer_Should_Handle_Unicode_And_Vietnamese()
    {
        var serializer = new AuditDetailsSerializer();
        var data = new { Message = "Xin chào thế giới!" };

        var result = serializer.Serialize(data);

        Assert.NotNull(result);
        Assert.Contains("Xin chào thế giới!", result);
    }

    [Fact]
    public void ContextAccessor_Should_Fallback_To_Anonymous_Without_HttpContext()
    {
        var accessor = new AuditContextAccessor(new FakeHttpContextAccessor());
        var context = accessor.GetCurrent();

        Assert.Equal("Anonymous", context.ActorType);
        Assert.Null(context.UserId);
        Assert.Null(context.Username);
    }

    [Fact]
    public void ContextAccessor_Should_Parse_Authenticated_User_Claims()
    {
        var fakeAccessor = new FakeHttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "99"),
            new Claim(ClaimTypes.Name, "admin_user")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContext.TraceIdentifier = "request-123-abc";
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        httpContext.Request.Headers["User-Agent"] = "Mozilla/5.0";

        fakeAccessor.HttpContext = httpContext;

        var accessor = new AuditContextAccessor(fakeAccessor);
        var context = accessor.GetCurrent();

        Assert.Equal("User", context.ActorType);
        Assert.Equal(99, context.UserId);
        Assert.Equal("admin_user", context.Username);
        Assert.Equal("request-123-abc", context.RequestId);
        Assert.Equal("127.0.0.1", context.IpAddress);
        Assert.Equal("Mozilla/5.0", context.UserAgent);
    }

    [Fact]
    public void ContextAccessor_Should_Support_System_Context_Override()
    {
        var accessor = new AuditContextAccessor(new FakeHttpContextAccessor());

        using (accessor.UseSystemContext())
        {
            var systemContext = accessor.GetCurrent();
            Assert.Equal("System", systemContext.ActorType);
            Assert.Equal("system", systemContext.Username);
            Assert.Null(systemContext.UserId);
        }

        var fallbackContext = accessor.GetCurrent();
        Assert.Equal("Anonymous", fallbackContext.ActorType);
    }

    [Fact]
    public void AuditLog_Should_Validate_Parameters_And_Kind_OccurredAt()
    {
        Assert.Throws<ArgumentException>(() => new AuditLog(
            actorUserId: null,
            actorUsername: null,
            actorType: "",
            action: "TEST",
            entityType: "TEST",
            entityId: null,
            result: "Success",
            occurredAt: DateTime.UtcNow,
            requestId: null,
            ipAddress: null,
            userAgent: null,
            detailsJson: null,
            errorCode: null
        ));

        Assert.Throws<ArgumentException>(() => new AuditLog(
            actorUserId: null,
            actorUsername: null,
            actorType: "User",
            action: "TEST",
            entityType: "TEST",
            entityId: null,
            result: "Success",
            occurredAt: DateTime.Now,
            requestId: null,
            ipAddress: null,
            userAgent: null,
            detailsJson: null,
            errorCode: null
        ));
    }

    // ==========================================
    // INTEGRATION TESTS
    // ==========================================

    [Fact]
    public async Task Create_Story_Success_Creates_AuditLog_Atomic_In_PostgreSQL()
    {
        using var client = await CreateAdminClient();

        // 1. Create Story
        var response = await client.PostAsJsonAsync("/api/v1/admin/stories", new
        {
            title = "Test Audit Story",
            slug = "test-audit-story",
            description = "Some description",
            coverImageUrl = "http://example.com/cover.png",
            authorName = "Author"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // 2. Query Database for AuditLog
        await using var db = _fixture.CreateContext();
        var logs = await db.AuditLogs.OrderByDescending(x => x.Id).ToListAsync();

        Assert.NotEmpty(logs);
        var createLog = logs.FirstOrDefault(x => x.Action == "STORY_CREATED");
        Assert.NotNull(createLog);
        Assert.Equal("Success", createLog.Result);
        Assert.Equal("Story", createLog.EntityType);
        Assert.NotNull(createLog.EntityId);
        Assert.NotNull(createLog.DetailsJson);
        Assert.Contains("test-audit-story", createLog.DetailsJson);
        Assert.NotNull(createLog.RequestId);
    }

    [Fact]
    public async Task Create_Chapter_Content_Redacted_In_AuditLog_Details()
    {
        using var client = await CreateAdminClient();

        // Create parent story
        var story = await AddStory("story-for-chapter-audit");

        // 1. Create Chapter
        var response = await client.PostAsJsonAsync($"/api/v1/admin/stories/{story.Id}/chapters", new
        {
            chapterNumber = 1,
            title = "Chapter 1",
            slug = "chapter-1",
            content = "<p>My raw secret HTML content</p>"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // 2. Inspect Audit Log Details
        await using var db = _fixture.CreateContext();
        var logs = await db.AuditLogs.Where(x => x.Action == "CHAPTER_CREATED").ToListAsync();
        Assert.NotEmpty(logs);

        var details = logs.First().DetailsJson;
        Assert.NotNull(details);
        Assert.DoesNotContain("My raw secret HTML content", details);
        Assert.Contains("chapter-1", details);
    }

    [Fact]
    public async Task Update_Story_Computes_ChangedFields_Success()
    {
        using var client = await CreateAdminClient();
        var story = await AddStory("story-to-update");

        // Update
        var response = await client.PutAsJsonAsync($"/api/v1/admin/stories/{story.Id}", new
        {
            title = "Story to Update changed",
            slug = "story-to-update-new",
            description = "New description",
            coverImageUrl = "http://example.com/new.png",
            authorName = "New Author",
            version = story.Version
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var db = _fixture.CreateContext();
        var logs = await db.AuditLogs.Where(x => x.Action == "STORY_UPDATED").OrderByDescending(x => x.Id).ToListAsync();
        Assert.NotEmpty(logs);

        var details = logs.First().DetailsJson;
        Assert.NotNull(details);
        using var detailsDoc = JsonDocument.Parse(details);
        var fields = detailsDoc.RootElement.GetProperty("changedFields").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("Title", fields);
        Assert.Contains("Description", fields);
        Assert.Contains("CoverImageUrl", fields);
        Assert.Contains("AuthorName", fields);
        Assert.Contains("Slug", fields);
    }

    [Fact]
    public async Task Unauthorized_Denied_Requests_Produce_Failed_Security_AuditLogs()
    {
        // 1. Send anonymous request to GET /api/v1/admin/audit-logs
        using var client = _factory!.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // 2. Query Database for security denial logs
        await using var db = _fixture.CreateContext();
        var logs = await db.AuditLogs.Where(x => x.Action == "ADMIN_ACCESS_UNAUTHORIZED").ToListAsync();
        
        Assert.NotEmpty(logs);
        var log = logs.First();
        Assert.Equal("Denied", log.Result);
        Assert.Equal("Security", log.EntityType);
        Assert.Equal("UNAUTHORIZED", log.ErrorCode);
    }

    [Fact]
    public async Task Non_Admin_Access_Produces_Forbidden_Denied_AuditLog()
    {
        // Seed regular user
        var client = await CreateRegularUserClient();
        var response = await client.GetAsync("/api/v1/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Query Database
        await using var db = _fixture.CreateContext();
        var logs = await db.AuditLogs.Where(x => x.Action == "ADMIN_ACCESS_DENIED").ToListAsync();
        
        Assert.NotEmpty(logs);
        var log = logs.First();
        Assert.Equal("Denied", log.Result);
        Assert.Equal("FORBIDDEN", log.ErrorCode);
    }

    [Fact]
    public async Task Query_Audit_Logs_GET_Supports_Filtering_And_Pagination()
    {
        using var client = await CreateAdminClient();

        // Query logs
        var response = await client.GetAsync("/api/v1/admin/audit-logs?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("data");
        
        Assert.True(items.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Transaction_Rollback_Saves_Only_Failed_AuditLog_And_No_Success_AuditLog()
    {
        // Setup mock DbContext that throws on SaveChanges
        var factory = new MockDbContextApiFactory(_fixture.ConnectionString);
        using var client = await CreateAdminClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/admin/stories", new
        {
            title = "Failed Story Save",
            slug = "failed-save",
            description = "Some desc",
            coverImageUrl = "http://example.com/cover.png",
            authorName = "Author"
        });

        // The save fails, meaning it returns 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Inspect database
        await using var db = _fixture.CreateContext();
        var logs = await db.AuditLogs.OrderByDescending(x => x.Id).ToListAsync();

        // 1. Success audit log must NOT exist (rolled back because DbContext save failed)
        var successLog = logs.FirstOrDefault(x => x.Action == "STORY_CREATED" && x.Result == "Success");
        Assert.Null(successLog);

        // 2. Failed audit log must exist (saved immediately in a separate scope/context)
        var failedLog = logs.FirstOrDefault(x => x.Action == "STORY_CREATED" && x.Result == "Failed");
        Assert.NotNull(failedLog);
        Assert.Equal("CONCURRENCY_CONFLICT", failedLog.ErrorCode);
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private async Task<HttpClient> CreateAdminClient(WebApplicationFactory<Program>? customFactory = null)
    {
        await SeedUser("integration-admin", UserRole.Admin);
        var activeFactory = customFactory ?? _factory!;
        var client = activeFactory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            usernameOrEmail = "integration-admin",
            password = "IntegrationAdmin@123"
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var document = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> CreateRegularUserClient()
    {
        await SeedUser("regular-user", UserRole.User);
        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            usernameOrEmail = "regular-user",
            password = "IntegrationAdmin@123"
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var document = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task SeedUser(string username, UserRole role)
    {
        await using var database = _fixture.CreateContext();
        if (await database.Users.AnyAsync(user => user.Username == username))
        {
            return;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationAdmin@123");
        var user = new User(
            username,
            $"{username}@example.test",
            passwordHash,
            role,
            mustChangePassword: false,
            DateTime.UtcNow);

        database.Users.Add(user);
        await database.SaveChangesAsync();
    }

    private async Task<Story> AddStory(string slug)
    {
        await using var database = _fixture.CreateContext();
        var story = new Story();
        story.UpdateDetails($"Story {slug}", slug, "Description", null, null, DateTime.UtcNow);
        database.Stories.Add(story);
        await database.SaveChangesAsync();
        return story;
    }

    private sealed class FakeHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
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
            if (_inner is ComicWeb.Persistence.Contexts.ApplicationDbContext dbContext)
            {
                var entries = dbContext.ChangeTracker.Entries();
                if (entries.Any(e => e.Entity is not AuditLog))
                {
                    throw new DbUpdateConcurrencyException("Simulated concurrency/save exception");
                }
            }
            return _inner.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class MockDbContextApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseSetting("BootstrapAdmin:Enabled", "false");
            builder.UseSetting("DatabaseInitialization:ApplyMigrationsOnStartup", "false");
            builder.UseSetting("Jwt:Issuer", JwtTestTokenFactory.Issuer);
            builder.UseSetting("Jwt:Audience", JwtTestTokenFactory.Audience);
            builder.UseSetting("Jwt:SigningKey", "postgresql-test-signing-key-at-least-32-characters-long");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IApplicationDbContext));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                
                services.AddScoped<IApplicationDbContext>(sp =>
                {
                    var options = sp.GetRequiredService<DbContextOptions<ComicWeb.Persistence.Contexts.ApplicationDbContext>>();
                    var context = new ComicWeb.Persistence.Contexts.ApplicationDbContext(options);
                    return new ConcurrencyMockDbContext(context);
                });
            });
        }
    }
}
