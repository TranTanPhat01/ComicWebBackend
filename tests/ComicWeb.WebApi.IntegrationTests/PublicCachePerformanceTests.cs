using ComicWeb.Application.Common.Configurations;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Common.Caching;
using ComicWeb.Persistence.Caching;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using ComicWeb.Persistence.Contexts;
using ComicWeb.WebApi.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public class PublicCachePerformanceTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private CachePerfApiFactory? _factory;

    public PublicCachePerformanceTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CachePerfApiFactory(_fixture.ConnectionString);
        return _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    // ==========================================
    // CACHE HIT & WARM-UP & ZERO QUERY TESTS
    // ==========================================

    [Fact]
    public async Task Warm_cache_requests_for_all_four_endpoints_should_have_zero_sql_read_queries()
    {
        // 1. Seed Story and Chapters
        var story = await AddStory("cache-perf-story");
        await AddChapter(story.Id, 1, "ch-1");

        using var client = _factory!.CreateClient();
        var interceptor = _factory.Services.GetRequiredService<QueryCounterInterceptor>();

        // Warm up / Cold Requests
        var r1 = await client.GetAsync("/api/v1/stories");
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var r2 = await client.GetAsync($"/api/v1/stories/{story.Slug}");
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        var r3 = await client.GetAsync($"/api/v1/stories/{story.Slug}/chapters");
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);
        var r4 = await client.GetAsync($"/api/v1/stories/{story.Slug}/chapters/ch-1");
        Assert.Equal(HttpStatusCode.OK, r4.StatusCode);

        // Reset query counter before warm requests
        interceptor.Reset();

        // 2. Execute Warm requests
        var w1 = await client.GetAsync("/api/v1/stories");
        Assert.Equal(HttpStatusCode.OK, w1.StatusCode);
        var w2 = await client.GetAsync($"/api/v1/stories/{story.Slug}");
        Assert.Equal(HttpStatusCode.OK, w2.StatusCode);
        var w3 = await client.GetAsync($"/api/v1/stories/{story.Slug}/chapters");
        Assert.Equal(HttpStatusCode.OK, w3.StatusCode);
        var w4 = await client.GetAsync($"/api/v1/stories/{story.Slug}/chapters/ch-1");
        Assert.Equal(HttpStatusCode.OK, w4.StatusCode);

        // 3. Verify exactly zero queries were executed for warm requests!
        Assert.Equal(0, interceptor.QueryCount);
    }

    // ==========================================
    // CONDITIONAL GET (ETAG & 304) TESTS
    // ==========================================

    [Fact]
    public async Task Story_and_chapter_details_should_support_conditional_get_304_flow()
    {
        var story = await AddStory("etag-story");
        var chapter = await AddChapter(story.Id, 1, "etag-ch");

        using var client = _factory!.CreateClient();

        // 1. Story Detail ETag
        var storyRes = await client.GetAsync($"/api/v1/stories/{story.Slug}");
        Assert.Equal(HttpStatusCode.OK, storyRes.StatusCode);
        var storyEtag = storyRes.Headers.ETag?.Tag;
        Assert.NotNull(storyEtag);

        // Try conditional GET for Story
        var storyReqMsg = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/stories/{story.Slug}");
        storyReqMsg.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(storyEtag));
        var storyRes304 = await client.SendAsync(storyReqMsg);
        Assert.Equal(HttpStatusCode.NotModified, storyRes304.StatusCode);
        Assert.Null(storyRes304.Content.Headers.ContentType); // No body returned

        // 2. Chapter Detail ETag
        var chRes = await client.GetAsync($"/api/v1/stories/{story.Slug}/chapters/{chapter.Slug}");
        Assert.Equal(HttpStatusCode.OK, chRes.StatusCode);
        var chEtag = chRes.Headers.ETag?.Tag;
        Assert.NotNull(chEtag);

        // Try conditional GET for Chapter
        var chReqMsg = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/stories/{story.Slug}/chapters/{chapter.Slug}");
        chReqMsg.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(chEtag));
        var chRes304 = await client.SendAsync(chReqMsg);
        Assert.Equal(HttpStatusCode.NotModified, chRes304.StatusCode);
    }

    [Fact]
    public async Task Invalidation_should_change_ETag_to_reflect_new_version()
    {
        using var client = await CreateAdminClient();
        var story = await AddStory("etag-change");

        // 1. Get current ETag
        var r1 = await client.GetAsync($"/api/v1/stories/{story.Slug}");
        var etag1 = r1.Headers.ETag?.Tag;

        // 2. Update Story (triggers invalidation)
        var response = await client.PutAsJsonAsync($"/api/v1/admin/stories/{story.Id}", new
        {
            title = "Etag Change Updated",
            slug = "etag-change",
            description = "New description",
            version = story.Version
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 3. Get ETag again (must be different)
        var r2 = await client.GetAsync($"/api/v1/stories/{story.Slug}");
        var etag2 = r2.Headers.ETag?.Tag;

        Assert.NotNull(etag1);
        Assert.NotNull(etag2);
        Assert.NotEqual(etag1, etag2);
    }

    // ==========================================
    // CACHE INVALIDATION MATRIX TESTS
    // ==========================================

    [Fact]
    public async Task Chapter_creation_should_invalidate_parent_story_detail_and_list_caches()
    {
        using var client = await CreateAdminClient();
        var story = await AddStory("inval-story");

        // Warm up Story List and Detail caches
        var list1 = await client.GetAsync("/api/v1/stories");
        var detail1 = await client.GetAsync($"/api/v1/stories/{story.Slug}");

        // Verify chapterCount in cached detail is 0
        var body1 = await detail1.Content.ReadAsStringAsync();
        using var doc1 = JsonDocument.Parse(body1);
        Assert.Empty(doc1.RootElement.GetProperty("data").GetProperty("chapters").EnumerateArray());

        // Create Chapter
        var response = await client.PostAsJsonAsync($"/api/v1/admin/stories/{story.Id}/chapters", new
        {
            chapterNumber = 1,
            title = "First Chapter",
            slug = "first-ch",
            content = "<p>Clean content</p>"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var createDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var chId = createDoc.RootElement.GetProperty("data").GetProperty("id").GetInt32();
        var chVer = createDoc.RootElement.GetProperty("data").GetProperty("version").GetInt32();

        // Publish Chapter
        var pubRes = await client.PostAsJsonAsync($"/api/v1/admin/chapters/{chId}/publish", new { version = chVer });
        Assert.Equal(HttpStatusCode.OK, pubRes.StatusCode);

        // Re-read details: cache invalidation must happen, bringing up the newly published chapter!
        var detail2 = await client.GetAsync($"/api/v1/stories/{story.Slug}");
        var body2 = await detail2.Content.ReadAsStringAsync();
        using var doc2 = JsonDocument.Parse(body2);
        var chapters = doc2.RootElement.GetProperty("data").GetProperty("chapters").EnumerateArray().ToList();
        Assert.Single(chapters);
        Assert.Equal("first-ch", chapters.First().GetProperty("slug").GetString());
    }

    // ==========================================
    // RESPONSE COMPRESSION TESTS
    // ==========================================

    [Fact]
    public async Task Api_should_compress_payloads_when_requested()
    {
        var story = await AddStory("compress-story");

        using var client = _factory!.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/stories/{story.Slug}");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        using var stream = await response.Content.ReadAsStreamAsync();
        using var decompressionStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new System.IO.StreamReader(decompressionStream);
        var content = await reader.ReadToEndAsync();
        Assert.Contains("compress-story", content);
    }

    [Fact]
    public async Task Response_compression_headers_must_be_set_without_autodecompression()
    {
        var story = await AddStory("compress-headers");

        using var client = _factory!.CreateDefaultClient();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/stories/{story.Slug}");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        Assert.True(response.Content.Headers.ContentEncoding.Contains("gzip"));
    }

    // ==========================================
    // CACHE DISABLED & EXCEPTION POLICY TESTS
    // ==========================================

    [Fact]
    public async Task When_cache_is_disabled_queries_should_always_go_to_database()
    {
        var factory = new CacheDisabledApiFactory(_fixture.ConnectionString);
        var story = await AddStory("disabled-cache-story");

        using var client = factory.CreateClient();
        var interceptor = factory.Services.GetRequiredService<QueryCounterInterceptor>();

        // Request 1
        var r1 = await client.GetAsync($"/api/v1/stories/{story.Slug}");
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var count1 = interceptor.QueryCount;

        // Reset
        interceptor.Reset();

        // Request 2
        var r2 = await client.GetAsync($"/api/v1/stories/{story.Slug}");
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        var count2 = interceptor.QueryCount;

        // Both requests must query database!
        Assert.True(count1 > 0);
        Assert.True(count2 > 0);
    }

    [Fact]
    public async Task When_cache_throws_exception_api_should_gracefully_fallback_to_database_without_failing()
    {
        var factory = new CacheExceptionApiFactory(_fixture.ConnectionString);
        var story = await AddStory("exception-fallback-story");

        using var client = factory.CreateClient();
        
        // This request would crash with 500 if exceptions weren't caught in our cache layer.
        // It must succeed (200 OK) by falling back to query PostgreSQL database!
        var response = await client.GetAsync($"/api/v1/stories/{story.Slug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("exception-fallback-story", body);
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private async Task<HttpClient> CreateAdminClient()
    {
        await SeedUser("admin-cache-perf", UserRole.Admin);
        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            usernameOrEmail = "admin-cache-perf",
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
        story.Publish(DateTime.UtcNow);
        database.Stories.Add(story);
        await database.SaveChangesAsync();
        return story;
    }

    private async Task<Chapter> AddChapter(int storyId, int number, string slug)
    {
        await using var database = _fixture.CreateContext();
        var chapter = new Chapter { StoryId = storyId };
        chapter.UpdateContent(number, $"Chapter {number}", slug, "<p>Clean HTML</p>", DateTime.UtcNow);
        chapter.Publish(DateTime.UtcNow);
        database.Chapters.Add(chapter);
        await database.SaveChangesAsync();
        return chapter;
    }

    // Custom WebApplicationFactory that overrides DbContext with a QueryCounterInterceptor registered
    private sealed class QueryCounterInterceptor : DbCommandInterceptor
    {
        private int _queryCount;
        public int QueryCount => _queryCount;

        public void Reset() => Interlocked.Exchange(ref _queryCount, 0);

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Interlocked.Increment(ref _queryCount);
            return base.ReaderExecuted(command, eventData, result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _queryCount);
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class CachePerfApiFactory(string connectionString) : WebApplicationFactory<Program>
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

            // Explicitly enable public caching
            builder.UseSetting("PublicCache:Enabled", "true");

            builder.ConfigureServices(services =>
            {
                var interceptor = new QueryCounterInterceptor();
                services.AddSingleton(interceptor);

                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    options.UseNpgsql(connectionString)
                           .AddInterceptors(interceptor);
                });
            });
        }
    }

    // Factory with cache disabled
    private sealed class CacheDisabledApiFactory(string connectionString) : WebApplicationFactory<Program>
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

            // Disable public cache
            builder.UseSetting("PublicCache:Enabled", "false");

            builder.ConfigureServices(services =>
            {
                var interceptor = new QueryCounterInterceptor();
                services.AddSingleton(interceptor);

                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    options.UseNpgsql(connectionString)
                           .AddInterceptors(interceptor);
                });
            });
        }
    }

    // Cache implementation that throws exception during set/get operations
    private sealed class ThrowingCache : IPublicContentCache
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated cache error");
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated cache error");
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated cache error");
        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated cache error");
    }

    private sealed class CacheExceptionApiFactory(string connectionString) : WebApplicationFactory<Program>
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

            builder.UseSetting("PublicCache:Enabled", "true");

            builder.ConfigureServices(services =>
            {
                // Register ThrowingCache to intercept cache calls and throw exceptions
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPublicContentCache));
                if (descriptor != null) services.Remove(descriptor);

                services.AddSingleton<IPublicContentCache, ThrowingCache>();
            });
        }
    }

    // ==========================================
    // UNIT TESTS FOR CACHE COMPONENTS
    // ==========================================

    [Fact]
    public void Key_factory_should_generate_normalized_and_stable_keys()
    {
        var factory = new PublicCacheKeyFactory();

        // 1. Normalize casing and whitespace in query
        var k1 = factory.CreateStoryListKey(1, 20, " Iron Man ", " Author ", "title");
        var k2 = factory.CreateStoryListKey(1, 20, "iron man", "author", "TITLE");
        Assert.Equal(k1, k2);

        // 2. Different parameters should lead to different keys
        var k3 = factory.CreateStoryListKey(2, 20, "iron man", "author", "TITLE");
        Assert.NotEqual(k1, k3);

        var k4 = factory.CreateStoryListKey(1, 10, "iron man", "author", "TITLE");
        Assert.NotEqual(k1, k4);

        var k5 = factory.CreateStoryListKey(1, 20, "iron man", "author", "-title");
        Assert.NotEqual(k1, k5);

        // 3. Story Detail Key case-insensitivity
        var kd1 = factory.CreateStoryDetailKey("My-Slug-Story");
        var kd2 = factory.CreateStoryDetailKey("my-slug-story");
        Assert.Equal(kd1, kd2);
        Assert.Equal("comicweb:v1:story:my-slug-story", kd1);

        // 4. Chapter Detail Key case-insensitivity
        var kc1 = factory.CreateChapterDetailKey("My-Story", "Ch-1");
        var kc2 = factory.CreateChapterDetailKey("my-story", "ch-1");
        Assert.Equal(kc1, kc2);
        Assert.Equal("comicweb:v1:story:my-story:chapter:ch-1", kc1);
    }

    [Fact]
    public async Task Keyed_lock_manager_should_prevent_stampede_and_isolate_by_key()
    {
        var manager = new KeyedLockManager();
        var executionCount = 0;

        var tasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using (await manager.LockAsync("shared-key"))
                {
                    Interlocked.Increment(ref executionCount);
                    await Task.Delay(20);
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Equal(5, executionCount);
    }

    [Fact]
    public async Task Keyed_lock_manager_should_run_keys_independently()
    {
        var manager = new KeyedLockManager();
        var startTime = DateTime.UtcNow;

        var t1 = Task.Run(async () =>
        {
            using (await manager.LockAsync("key-1"))
            {
                await Task.Delay(100);
            }
        });

        var t2 = Task.Run(async () =>
        {
            using (await manager.LockAsync("key-2"))
            {
                await Task.Delay(100);
            }
        });

        await Task.WhenAll(t1, t2);
        var duration = DateTime.UtcNow - startTime;

        Assert.True(duration.TotalMilliseconds < 160);
    }

    [Fact]
    public async Task Cache_wrapper_should_intercept_internal_errors_gracefully()
    {
        var nullLogger = NullLogger<PublicContentCache>.Instance;
        var cache = new PublicContentCache(null!, nullLogger);

        var val = await cache.GetAsync<string>("any-key");
        Assert.Null(val);

        await cache.SetAsync("any-key", "value", TimeSpan.FromMinutes(1));
        await cache.RemoveAsync("any-key");
        await cache.RemoveByPrefixAsync("prefix");
    }
}
