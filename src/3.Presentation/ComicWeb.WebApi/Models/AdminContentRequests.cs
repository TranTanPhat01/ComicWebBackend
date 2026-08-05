namespace ComicWeb.WebApi.Models;

public sealed record StoryUpsertRequest(string Title, string? Slug, string Description, string? CoverImageUrl, string? AuthorName, IReadOnlyList<string>? Genres, int? Version);
public sealed record ChapterUpsertRequest(int ChapterNumber, string Title, string? Slug, string Content, int? Version, bool IsLocked = false, string? AffiliateLink = null);
public sealed record GenreUpsertRequest(string Name, string? Slug, string? Description, bool IsActive = true);
public sealed record VersionRequest(int? Version);
public sealed record ScheduleRequest(System.DateTime? ScheduledAt, int? Version);
public sealed record PagedApiEnvelope<T>(IReadOnlyList<T> Data, object Meta, string RequestId);
