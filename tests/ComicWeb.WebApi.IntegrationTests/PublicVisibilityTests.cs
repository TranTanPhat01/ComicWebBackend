using System.Net;
using System.Text.Json;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public sealed class PublicVisibilityTests(PostgreSqlFixture fixture) : IAsyncLifetime
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
    public async Task Public_list_returns_only_published_and_completed_stories()
    {
        await SeedStory("published", StoryStatus.Published);
        await SeedStory("completed", StoryStatus.Completed);
        await SeedStory("draft", StoryStatus.Draft);
        await SeedStory("hidden", StoryStatus.Hidden);
        var deleted = await SeedStory("deleted", StoryStatus.Published);
        await SoftDeleteStory(deleted.Id);

        using var client = _factory!.CreateClient();
        using var response = await client.GetAsync("/api/v1/stories");
        var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var slugs = body.RootElement.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("slug").GetString()).ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("published", slugs);
        Assert.Contains("completed", slugs);
        Assert.DoesNotContain("draft", slugs);
        Assert.DoesNotContain("hidden", slugs);
        Assert.DoesNotContain("deleted", slugs);
    }

    [Theory]
    [InlineData("draft", HttpStatusCode.NotFound)]
    [InlineData("hidden", HttpStatusCode.NotFound)]
    [InlineData("missing", HttpStatusCode.NotFound)]
    public async Task Private_or_unknown_story_detail_returns_not_found(string slug, HttpStatusCode expected)
    {
        await SeedStory("draft", StoryStatus.Draft);
        await SeedStory("hidden", StoryStatus.Hidden);
        using var client = _factory!.CreateClient();
        using var response = await client.GetAsync($"/api/v1/stories/{slug}");
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Public_chapter_detail_hides_private_chapters_and_private_parent_story()
    {
        var story = await SeedStory("public-story", StoryStatus.Published);
        await SeedChapter(story.Id, 1, "published", ChapterStatus.Published);
        await SeedChapter(story.Id, 2, "draft-chapter", ChapterStatus.Draft);
        var privateStory = await SeedStory("private-story", StoryStatus.Draft);
        await SeedChapter(privateStory.Id, 1, "published-under-private", ChapterStatus.Published);

        using var client = _factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/stories/public-story/chapters/published")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stories/public-story/chapters/draft-chapter")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stories/private-story/chapters/published-under-private")).StatusCode);
    }

    private async Task<Story> SeedStory(string slug, StoryStatus status)
    {
        var story = new Story { Status = status };
        story.UpdateDetails(slug, slug, "description", null, null, DateTime.UtcNow);
        await using var database = fixture.CreateContext();
        database.Stories.Add(story);
        await database.SaveChangesAsync();
        return story;
    }

    private async Task SoftDeleteStory(int storyId)
    {
        await using var database = fixture.CreateContext();
        var story = await database.Stories.SingleAsync(x => x.Id == storyId);
        story.SoftDelete(DateTime.UtcNow);
        await database.SaveChangesAsync();
    }

    private async Task SeedChapter(int storyId, int number, string slug, ChapterStatus status)
    {
        var chapter = new Chapter { StoryId = storyId };
        chapter.UpdateContent(number, slug, slug, "content", DateTime.UtcNow);
        if (status == ChapterStatus.Published) chapter.Publish(DateTime.UtcNow);
        if (status == ChapterStatus.Hidden) { chapter.Publish(DateTime.UtcNow); chapter.Hide(DateTime.UtcNow); }
        await using var database = fixture.CreateContext();
        database.Chapters.Add(chapter);
        await database.SaveChangesAsync();
    }
}
