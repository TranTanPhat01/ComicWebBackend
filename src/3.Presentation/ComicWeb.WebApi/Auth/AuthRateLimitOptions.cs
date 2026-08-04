namespace ComicWeb.WebApi.Auth;

public sealed class AuthRateLimitOptions { public const string SectionName = "AuthRateLimit"; public int LoginPerMinute { get; init; } = 5; public int RefreshPerMinute { get; init; } = 20; }
