using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;

namespace ComicWeb.Application.Tests;

public sealed class PublishWorkflowDomainTests
{
    [Fact]
    public void Story_republish_preserves_first_published_at()
    {
        var story = ValidStory();
        story.Publish(DateTime.UtcNow.AddMinutes(-5));
        var first = story.PublishedAt;
        story.Unpublish(DateTime.UtcNow.AddMinutes(-1));
        story.Publish(DateTime.UtcNow);
        Assert.Equal(first, story.PublishedAt);
        Assert.Equal(StoryStatus.Published, story.Status);
    }

    [Fact]
    public void Chapter_cannot_publish_empty_content()
    {
        var chapter = new Chapter { ChapterNumber = 1, Title = "One", Slug = "one", Content = " " };
        Assert.Throws<InvalidOperationException>(() => chapter.Publish(DateTime.UtcNow));
    }

    [Fact]
    public void Chapter_republish_preserves_first_published_at()
    {
        var chapter = new Chapter { ChapterNumber = 1, Title = "One", Slug = "one", Content = "content" };
        chapter.Publish(DateTime.UtcNow.AddMinutes(-5));
        var first = chapter.PublishedAt;
        chapter.Unpublish(DateTime.UtcNow.AddMinutes(-1));
        chapter.Publish(DateTime.UtcNow);
        Assert.Equal(first, chapter.PublishedAt);
        Assert.Equal(ChapterStatus.Published, chapter.Status);
    }

    private static Story ValidStory() => new() { Title = "Story", Slug = "story" };
}
