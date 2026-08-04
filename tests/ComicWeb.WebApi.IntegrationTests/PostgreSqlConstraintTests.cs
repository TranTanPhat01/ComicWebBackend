using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ComicWeb.WebApi.IntegrationTests;

[Collection("PostgreSql")]
public sealed class PostgreSqlConstraintTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Active_story_slug_is_unique_but_can_be_reused_after_soft_delete()
    {
        await using var database = fixture.CreateContext();
        var deleted = Story("same-slug");
        database.Stories.Add(deleted);
        await database.SaveChangesAsync();
        deleted.SoftDelete(DateTime.UtcNow);
        await database.SaveChangesAsync();

        database.Stories.Add(Story("same-slug"));
        await database.SaveChangesAsync();
    }

    [Fact]
    public async Task Active_chapter_number_is_unique_per_story()
    {
        await using var database = fixture.CreateContext();
        var story = Story("story-one");
        database.Stories.Add(story);
        await database.SaveChangesAsync();

        database.Chapters.Add(Chapter(story.Id, 1, "one"));
        await database.SaveChangesAsync();
        database.Chapters.Add(Chapter(story.Id, 1, "two"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, GetPostgresException(exception).SqlState);
    }

    [Fact]
    public async Task Story_slug_unique_violation_has_postgresql_sqlstate()
    {
        await using var database = fixture.CreateContext();
        database.Stories.AddRange(Story("same"), Story("same"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, GetPostgresException(exception).SqlState);
    }

    [Fact]
    public async Task Chapter_number_and_slug_can_be_reused_after_soft_delete_and_across_stories()
    {
        await using var database = fixture.CreateContext();
        var first = Story("first");
        var second = Story("second");
        database.Stories.AddRange(first, second);
        await database.SaveChangesAsync();

        var deleted = Chapter(first.Id, 1, "chapter");
        database.Chapters.Add(deleted);
        await database.SaveChangesAsync();
        deleted.SoftDelete(DateTime.UtcNow);
        await database.SaveChangesAsync();

        database.Chapters.AddRange(Chapter(first.Id, 1, "chapter"), Chapter(second.Id, 1, "chapter"));
        await database.SaveChangesAsync();
    }

    [Fact]
    public async Task Foreign_key_and_restrict_delete_are_enforced_by_postgresql()
    {
        await using var database = fixture.CreateContext();
        database.Chapters.Add(Chapter(99999, 1, "orphan"));
        var foreignKey = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, GetPostgresException(foreignKey).SqlState);

        database.ChangeTracker.Clear();
        var story = Story("restricted");
        database.Stories.Add(story);
        await database.SaveChangesAsync();
        var chapter = Chapter(story.Id, 1, "chapter");
        database.Chapters.Add(chapter);
        await database.SaveChangesAsync();

        var restrict = await Assert.ThrowsAsync<PostgresException>(
            () => database.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"Stories\" WHERE \"Id\" = {0}",
                story.Id));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, restrict.SqlState);
    }

    [Fact]
    public async Task Enum_statuses_are_persisted_as_strings()
    {
        await using var database = fixture.CreateContext();
        var story = Story("enum");
        story.Status = StoryStatus.Completed;
        database.Stories.Add(story);
        await database.SaveChangesAsync();
        var chapter = Chapter(story.Id, 1, "enum-chapter");
        chapter.Publish(DateTime.UtcNow);
        database.Chapters.Add(chapter);
        await database.SaveChangesAsync();

        var values = await database.Database.SqlQueryRaw<string>("SELECT \"Status\" AS \"Value\" FROM \"Stories\" WHERE \"Id\" = {0}", story.Id).ToListAsync();
        var chapterValues = await database.Database.SqlQueryRaw<string>("SELECT \"Status\" AS \"Value\" FROM \"Chapters\" WHERE \"Id\" = {0}", chapter.Id).ToListAsync();
        Assert.Equal("Completed", Assert.Single(values));
        Assert.Equal("Published", Assert.Single(chapterValues));
    }

    private static PostgresException GetPostgresException(DbUpdateException exception)
        => Assert.IsType<PostgresException>(exception.InnerException);

    private static Story Story(string slug)
    {
        var story = new Story();
        story.UpdateDetails("Story " + slug, slug, "description", null, null, DateTime.UtcNow);
        return story;
    }

    private static Chapter Chapter(int storyId, int number, string slug)
    {
        var chapter = new Chapter { StoryId = storyId };
        chapter.UpdateContent(number, "Chapter " + slug, slug, "content", DateTime.UtcNow);
        return chapter;
    }
}
