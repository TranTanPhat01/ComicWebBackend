using System.Net;
using System.Text.Json;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public sealed class PublicReadingQueryTests(PostgreSqlFixture fixture) : IAsyncLifetime
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
    public async Task Pagination_counts_only_public_stories_and_validates_input()
    {
        await SeedPublicStories();
        await AddStory("draft", "Draft story", StoryStatus.Draft, DateTime.UtcNow);
        await AddStory("hidden", "Hidden story", StoryStatus.Hidden, DateTime.UtcNow);
        var deleted = await AddStory("deleted", "Deleted story", StoryStatus.Published, DateTime.UtcNow);
        await SoftDeleteStory(deleted.Id);

        using var client = _factory!.CreateClient();
        using var first = await GetDocument(client, "/api/v1/stories?page=1&pageSize=3&sort=title");
        using var second = await GetDocument(client, "/api/v1/stories?page=2&pageSize=3&sort=title");
        using var empty = await GetDocument(client, "/api/v1/stories?page=99&pageSize=3");

        AssertPage(first, page: 1, pageSize: 3, totalItems: 7, totalPages: 3, expectedItems: 3);
        AssertPage(second, page: 2, pageSize: 3, totalItems: 7, totalPages: 3, expectedItems: 3);
        AssertPage(empty, page: 99, pageSize: 3, totalItems: 7, totalPages: 3, expectedItems: 0);

        var firstSlugs = Slugs(first).ToHashSet();
        Assert.Empty(firstSlugs.Intersect(Slugs(second)));

        await AssertProblem(client, "/api/v1/stories?page=0", "INVALID_PAGE");
        await AssertProblem(client, "/api/v1/stories?pageSize=0", "INVALID_PAGE");
        await AssertProblem(client, "/api/v1/stories?pageSize=101", "INVALID_PAGE");
    }

    [Fact]
    public async Task Search_filters_public_dataset_and_combines_with_sort_and_pagination()
    {
        await AddStory("hero-alpha", "Hero Alpha", StoryStatus.Published, DateTime.UtcNow.AddMinutes(-7), "Author One");
        await AddStory("hero-bravo", "Hero Bravo", StoryStatus.Completed, DateTime.UtcNow.AddMinutes(-6), "Author Two");
        await AddStory("hero-charlie", "Hero Charlie", StoryStatus.Published, DateTime.UtcNow.AddMinutes(-5), "Author Three");
        await AddStory("private-hero", "Hero Private", StoryStatus.Draft, DateTime.UtcNow, "Author One");
        await AddStory("hidden-hero", "Hero Hidden", StoryStatus.Hidden, DateTime.UtcNow, "Author One");
        var deletedHero = await AddStory("deleted-hero", "Hero Deleted", StoryStatus.Published, DateTime.UtcNow, "Author One");
        await SoftDeleteStory(deletedHero.Id);
        await AddStory("tieng-viet", "Truyện Việt", StoryStatus.Published, DateTime.UtcNow.AddMinutes(-4), "Tác giả");

        using var client = _factory!.CreateClient();
        using var document = await GetDocument(client, "/api/v1/stories?query=%20hErO%20&sort=-title&page=2&pageSize=1");
        AssertPage(document, page: 2, pageSize: 1, totalItems: 3, totalPages: 3, expectedItems: 1);
        Assert.Equal("hero-bravo", Assert.Single(Slugs(document)));

        using var noMatch = await GetDocument(client, "/api/v1/stories?query=not-found");
        AssertPage(noMatch, page: 1, pageSize: 20, totalItems: 0, totalPages: 0, expectedItems: 0);

        using var unicode = await GetDocument(client, "/api/v1/stories?query=vi%E1%BB%87t");
        Assert.Contains("tieng-viet", Slugs(unicode));
    }

    [Theory]
    [InlineData("title", false)]
    [InlineData("-title", true)]
    [InlineData("updatedAt", false)]
    [InlineData("-updatedAt", true)]
    [InlineData("publishedAt", false)]
    [InlineData("-publishedAt", true)]
    public async Task Every_supported_story_sort_orders_the_public_result(string sort, bool descending)
    {
        await SeedPublicStories();
        await AddStory("private-sort", "Private sort", StoryStatus.Hidden, DateTime.UtcNow);

        using var client = _factory!.CreateClient();
        using var document = await GetDocument(client, $"/api/v1/stories?sort={Uri.EscapeDataString(sort)}&pageSize=20");
        var data = document.RootElement.GetProperty("data").EnumerateArray().ToArray();
        var field = sort.Contains("title", StringComparison.Ordinal) ? "title" : sort.Contains("updated", StringComparison.Ordinal) ? "updatedAt" : "publishedAt";
        var values = data.Select(item => item.GetProperty(field).GetString()!).ToArray();
        var expected = descending
            ? values.OrderByDescending(value => value, StringComparer.Ordinal).ToArray()
            : values.OrderBy(value => value, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, values);
        Assert.DoesNotContain("private-sort", Slugs(document));

        await AssertProblem(client, "/api/v1/stories?sort=unknown", "INVALID_SORT");
    }

    private async Task SeedPublicStories()
    {
        var origin = DateTime.UtcNow.AddMinutes(-20);
        for (var index = 1; index <= 7; index++)
        {
            var status = index % 2 == 0 ? StoryStatus.Completed : StoryStatus.Published;
            await AddStory($"public-{index}", $"Public {index}", status, origin.AddMinutes(index));
        }
    }

    private async Task<Story> AddStory(
        string slug,
        string title,
        StoryStatus status,
        DateTime publishedAt,
        string? author = null)
    {
        await using var database = fixture.CreateContext();
        var story = new Story();
        story.UpdateDetails(title, slug, "Description", null, author, publishedAt.AddMinutes(-1));
        if (status is StoryStatus.Published or StoryStatus.Completed)
        {
            story.Publish(publishedAt);
        }

        if (status == StoryStatus.Completed)
        {
            story.Complete(publishedAt.AddSeconds(1));
        }

        if (status == StoryStatus.Hidden)
        {
            story.Publish(publishedAt);
            story.Hide(publishedAt.AddSeconds(1));
        }

        database.Stories.Add(story);
        await database.SaveChangesAsync();
        return story;
    }

    private async Task SoftDeleteStory(int id)
    {
        await using var database = fixture.CreateContext();
        var story = await database.Stories.SingleAsync(item => item.Id == id);
        story.SoftDelete(DateTime.UtcNow);
        await database.SaveChangesAsync();
    }

    private static async Task<JsonDocument> GetDocument(HttpClient client, string route)
    {
        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertProblem(HttpClient client, string route, string code)
    {
        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }

    private static void AssertPage(
        JsonDocument document,
        int page,
        int pageSize,
        int totalItems,
        int totalPages,
        int expectedItems)
    {
        var root = document.RootElement;
        var meta = root.GetProperty("meta");
        Assert.Equal(page, meta.GetProperty("page").GetInt32());
        Assert.Equal(pageSize, meta.GetProperty("pageSize").GetInt32());
        Assert.Equal(totalItems, meta.GetProperty("totalItems").GetInt32());
        Assert.Equal(totalPages, meta.GetProperty("totalPages").GetInt32());
        Assert.Equal(expectedItems, root.GetProperty("data").GetArrayLength());
    }

    private static IEnumerable<string> Slugs(JsonDocument document)
    {
        return document.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("slug").GetString()!);
    }
}
