using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Persistence.Auth;
using Microsoft.Extensions.Options;

namespace ComicWeb.WebApi.Auth;

public sealed class LoginRequestRateLimiter(IOptions<AuthenticationSecurityOptions> options)
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _attempts = new();
    private readonly AuthenticationSecurityOptions _options = options.Value;

    public void Check(string? ipAddress, string usernameOrEmail, DateTime now)
    {
        CheckPartition($"ip:{ipAddress ?? "unknown"}", _options.LoginRequestsPerMinute, TimeSpan.FromMinutes(1), now);
        var identityHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(usernameOrEmail.Trim().ToUpperInvariant())));
        CheckPartition($"identity:{identityHash}", _options.LoginRequestsPerMinute, TimeSpan.FromMinutes(1), now);
        CheckPartition($"identity-hour:{identityHash}", _options.LoginRequestsPerHour, TimeSpan.FromHours(1), now);
    }

    private void CheckPartition(string key, int limit, TimeSpan window, DateTime now)
    {
        var attempts = _attempts.GetOrAdd(key, _ => new Queue<DateTime>());
        lock (attempts)
        {
            while (attempts.Count > 0 && attempts.Peek() <= now - window) attempts.Dequeue();
            if (attempts.Count >= limit) throw new AppException("RATE_LIMITED", 429, "Too many requests", "Too many authentication attempts. Please try again later.");
            attempts.Enqueue(now);
        }
    }
}
