namespace ComicWeb.Application.Common.Configurations;

public sealed class PublicCacheOptions
{
    public const string SectionName = "PublicCache";

    public bool Enabled { get; init; } = true;

    public int StoryListTtlSeconds { get; init; } = 30;

    public int StoryDetailTtlSeconds { get; init; } = 120;

    public int ChapterListTtlSeconds { get; init; } = 120;

    public int ChapterDetailTtlSeconds { get; init; } = 600;

    public int MaxEntrySizeBytes { get; init; } = 1_000_000;
}
