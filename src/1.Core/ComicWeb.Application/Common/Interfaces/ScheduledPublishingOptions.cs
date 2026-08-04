namespace ComicWeb.Application.Common.Interfaces;

public sealed class ScheduledPublishingOptions
{
    public const string SectionName = "ScheduledPublishing";

    public bool Enabled { get; init; } = false;
    public int IntervalSeconds { get; init; } = 30;
    public int BatchSize { get; init; } = 100;
}
