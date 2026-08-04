namespace ComicWeb.Persistence.Contexts;

public sealed class DatabaseInitializationOptions
{
    public const string SectionName = "DatabaseInitialization";

    public bool ApplyMigrationsOnStartup { get; init; }

    public int MaxRetryAttempts { get; init; } = 5;

    public int RetryDelaySeconds { get; init; } = 2;
}
