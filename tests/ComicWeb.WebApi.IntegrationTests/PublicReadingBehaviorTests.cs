using System.Net;
using System.Text.Json;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public sealed class PublicReadingBehaviorTests(PostgreSqlFixture fixture) : IAsyncLifetime
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
    public async Task Story_details_return_public_statuses_and_hide_private_ones()
    {
        await AddStory("published", StoryStatus.Published);
        await AddStory("completed", StoryStatus.Completed);
        await AddStory("draft", StoryStatus.Draft);
        await AddStory("hidden", StoryStatus.Hidden);
        var deleted = await AddStory("deleted", StoryStatus.Published);
        await SoftDeleteStory(deleted.Id);

        using var client = _factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/stories/published")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/stories/completed")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stories/draft")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stories/hidden")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stories/deleted")).StatusCode);
    }

    [Fact]
    public async Task Chapter_list_and_detail_exclude_non_public_chapters_and_parents()
    {
        var publicStory = await AddStory("public", StoryStatus.Published);
        await AddChapter(publicStory.Id, 1, "published", ChapterStatus.Published);
        await AddChapter(publicStory.Id, 2, "draft", ChapterStatus.Draft);
        await AddChapter(publicStory.Id, 3, "hidden", ChapterStatus.Hidden);
        var deleted = await AddChapter(publicStory.Id, 4, "deleted", ChapterStatus.Published);
        await SoftDeleteChapter(deleted.Id);
        var completed = await AddStory("completed-parent", StoryStatus.Completed);
        await AddChapter(completed.Id, 1, "completed-visible", ChapterStatus.Published);
        var hiddenParent = await AddStory("hidden-parent", StoryStatus.Hidden);
        await AddChapter(hiddenParent.Id, 1, "hidden-parent-chapter", ChapterStatus.Published);

        using var client = _factory!.CreateClient();
        using var list = await JsonDocument.ParseAsync(await (await client.GetAsync("/api/v1/stories/public/chapters")).Content.ReadAsStreamAsync());
        var slugs = list.RootElement.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("slug").GetString()).ToArray();
        Assert.Equal(new[] { "published" }, slugs);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/stories/completed-parent/chapters/completed-visible")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stories/public/chapters/draft")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stories/public/chapters/hidden")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stories/public/chapters/deleted")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stories/hidden-parent/chapters/hidden-parent-chapter")).StatusCode);
    }

    [Fact]
    public async Task Navigation_skips_draft_and_deleted_chapters_and_reacts_to_hide()
    {
        var story = await AddStory("navigation", StoryStatus.Published);
        await AddChapter(story.Id, 1, "one", ChapterStatus.Published);
        await AddChapter(story.Id, 3, "three", ChapterStatus.Published);
        await AddChapter(story.Id, 5, "five", ChapterStatus.Draft);
        var deleted = await AddChapter(story.Id, 6, "six", ChapterStatus.Published);
        await SoftDeleteChapter(deleted.Id);
        await AddChapter(story.Id, 7, "seven", ChapterStatus.Published);

        await AssertNavigation("navigation", "one", null, 3);
        await AssertNavigation("navigation", "three", 1, 7);
        await AssertNavigation("navigation", "seven", 3, null);
        await HideChapter(story.Id, "three");
        await AssertNavigation("navigation", "one", null, 7);
        await AssertNavigation("navigation", "seven", 1, null);
        using var client = _factory!.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stories/navigation/chapters/three")).StatusCode);
    }

    [Fact]
    public async Task Public_projection_counts_only_published_chapters_and_updates_after_mutation()
    {
        var story = await AddStory("projection", StoryStatus.Published);
        await AddChapter(story.Id, 1, "one", ChapterStatus.Published);
        await AddChapter(story.Id, 2, "two", ChapterStatus.Draft);
        await AddChapter(story.Id, 3, "three", ChapterStatus.Hidden);
        await AddChapter(story.Id, 4, "four", ChapterStatus.Published);
        var deleted = await AddChapter(story.Id, 5, "five", ChapterStatus.Published);
        await SoftDeleteChapter(deleted.Id);

        await AssertProjection("projection", 2, 4);
        await HideChapter(story.Id, "four");
        await AssertProjection("projection", 1, 1);
        await HideChapter(story.Id, "one");
        await AssertProjection("projection", 0, null);
        await PublishChapter(story.Id, "four");
        await AssertProjection("projection", 1, 4);
    }

    private async Task AssertNavigation(string storySlug, string chapterSlug, int? previous, int? next)
    {
        using var client = _factory!.CreateClient();
        using var document = await JsonDocument.ParseAsync(await (await client.GetAsync($"/api/v1/stories/{storySlug}/chapters/{chapterSlug}")).Content.ReadAsStreamAsync());
        var data = document.RootElement.GetProperty("data");
        AssertNumber(data, "previousChapter", previous);
        AssertNumber(data, "nextChapter", next);
    }

    private async Task AssertProjection(string slug, int count, int? latest)
    {
        using var client = _factory!.CreateClient();
        using var list = await JsonDocument.ParseAsync(await (await client.GetAsync("/api/v1/stories")).Content.ReadAsStreamAsync());
        var item = list.RootElement.GetProperty("data").EnumerateArray().Single(x => x.GetProperty("slug").GetString() == slug);
        Assert.Equal(count, item.GetProperty("chapterCount").GetInt32());
        AssertNumber(item, "latestChapter", latest);
        using var detail = await JsonDocument.ParseAsync(await (await client.GetAsync($"/api/v1/stories/{slug}")).Content.ReadAsStreamAsync());
        Assert.Equal(count, detail.RootElement.GetProperty("data").GetProperty("chapters").GetArrayLength());
    }

    private static void AssertNumber(JsonElement parent, string property, int? expected)
    {
        var element = parent.GetProperty(property);
        if (expected is null) Assert.Equal(JsonValueKind.Null, element.ValueKind);
        else Assert.Equal(expected.Value, element.GetProperty("number").GetInt32());
    }

    private async Task<Story> AddStory(string slug, StoryStatus status)
    {
        var story = new Story { Status = status };
        story.UpdateDetails(slug, slug, "description", null, null, DateTime.UtcNow);
        await using var database = fixture.CreateContext();
        database.Stories.Add(story);
        await database.SaveChangesAsync();
        return story;
    }

    private async Task<Chapter> AddChapter(int storyId, int number, string slug, ChapterStatus status)
    {
        var chapter = new Chapter { StoryId = storyId };
        chapter.UpdateContent(number, slug, slug, "content", DateTime.UtcNow);
        if (status == ChapterStatus.Published) chapter.Publish(DateTime.UtcNow);
        if (status == ChapterStatus.Hidden) { chapter.Publish(DateTime.UtcNow); chapter.Hide(DateTime.UtcNow); }
        await using var database = fixture.CreateContext();
        database.Chapters.Add(chapter);
        await database.SaveChangesAsync();
        return chapter;
    }

    private async Task SoftDeleteStory(int id) { await using var db = fixture.CreateContext(); var entity = await db.Stories.SingleAsync(x => x.Id == id); entity.SoftDelete(DateTime.UtcNow); await db.SaveChangesAsync(); }
    private async Task SoftDeleteChapter(int id) { await using var db = fixture.CreateContext(); var entity = await db.Chapters.SingleAsync(x => x.Id == id); entity.SoftDelete(DateTime.UtcNow); await db.SaveChangesAsync(); }
    private async Task HideChapter(int storyId, string slug) { await using var db = fixture.CreateContext(); var entity = await db.Chapters.SingleAsync(x => x.StoryId == storyId && x.Slug == slug); entity.Hide(DateTime.UtcNow); await db.SaveChangesAsync(); }
    private async Task PublishChapter(int storyId, string slug) { await using var db = fixture.CreateContext(); var entity = await db.Chapters.SingleAsync(x => x.StoryId == storyId && x.Slug == slug); entity.Publish(DateTime.UtcNow); await db.SaveChangesAsync(); }
}
