using ComicWeb.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace ComicWeb.Persistence.Auth;

public sealed class AuthenticationSecurityOptions
{
    public const string SectionName = "AuthenticationSecurity";
    public int MaxFailedLoginAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;
    public int LoginRequestsPerMinute { get; init; } = 5;
    public int LoginRequestsPerHour { get; init; } = 20;
    public int RefreshRequestsPerMinute { get; init; } = 10;
}

public sealed class AuthenticationSecurityPolicy(IOptions<AuthenticationSecurityOptions> options) : IAuthenticationSecurityPolicy
{
    private readonly AuthenticationSecurityOptions _options = options.Value;
    public int MaxFailedLoginAttempts => _options.MaxFailedLoginAttempts;
    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(_options.LockoutMinutes);
}
