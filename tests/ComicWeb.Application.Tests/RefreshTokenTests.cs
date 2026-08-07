using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Auth;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ComicWeb.Application.Tests;

public class RefreshTokenTests
{
    private class FakeAuthRepository : IAuthRepository
    {
        public List<User> Users { get; } = new();
        public List<RefreshSession> Sessions { get; } = new();
        public List<string> RevokedUserSessionsReasons { get; } = new();

        public Task<User?> FindByUsernameOrEmailAsync(string normalizedValue, CancellationToken ct) => Task.FromResult<User?>(null);

        public Task<User?> FindByIdAsync(int id, CancellationToken ct)
        {
            var u = Users.Find(x => x.Id == id);
            return Task.FromResult(u);
        }

        public Task<bool> HasAdministratorAsync(CancellationToken ct) => Task.FromResult(false);
        public Task AddUserAsync(User user, CancellationToken ct) => Task.CompletedTask;

        public Task<RefreshSession?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken ct)
        {
            var s = Sessions.Find(x => x.TokenHash == tokenHash);
            return Task.FromResult(s);
        }

        public Task AddSessionAsync(RefreshSession session, CancellationToken ct)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task RevokeAllSessionsAsync(int userId, DateTime now, string? ip, string reason, CancellationToken ct)
        {
            RevokedUserSessionsReasons.Add($"{userId}:{reason}");
            foreach (var session in Sessions.FindAll(x => x.UserId == userId))
            {
                session.Revoke(now, ip, reason);
            }
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private class FakeTokenService : ITokenService
    {
        private int _counter = 0;

        public AccessToken CreateAccessToken(User user, Guid sessionId, DateTime now)
        {
            return new AccessToken("access_token_" + user.Id, Guid.NewGuid().ToString("N"), now.AddMinutes(15));
        }

        public string CreateRefreshToken()
        {
            _counter++;
            return "refresh_token_" + _counter;
        }

        public string HashRefreshToken(string token)
        {
            return "hash_" + token;
        }
    }

    private class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task Refresh_WithValidActiveToken_ShouldRotateAndReturnNewTokens()
    {
        var repo = new FakeAuthRepository();
        var tokens = new FakeTokenService();
        var clock = new FakeDateTimeProvider();

        var user = new User("tester", "tester@test.com", "hash", UserRole.User, false, clock.UtcNow) { Id = 42 };
        repo.Users.Add(user);

        // Active session
        var oldRefreshToken = "refresh_token_old";
        var oldSession = new RefreshSession(
            userId: user.Id,
            tokenHash: tokens.HashRefreshToken(oldRefreshToken),
            jwtId: "jwt1",
            now: clock.UtcNow.AddHours(-1),
            expiresAt: clock.UtcNow.AddDays(7),
            ip: "127.0.0.1",
            userAgent: "Mozilla"
        );
        repo.Sessions.Add(oldSession);

        var handler = new RefreshHandler(repo, tokens, clock);
        var command = new RefreshCommand(oldRefreshToken, "127.0.0.1", "Mozilla");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.StartsWith("access_token_", result.AccessToken);
        Assert.Equal("refresh_token_1", result.RefreshToken); // first new token generated
        Assert.Equal(clock.UtcNow.AddDays(7), result.RefreshExpiresAt);

        // Verify old session revoked (rotated)
        Assert.NotNull(oldSession.RevokedAt);
        Assert.Equal("rotated", oldSession.RevokeReason);
        Assert.Equal(repo.Sessions[1].Id, oldSession.ReplacedBySessionId); // Next session ID

        // Verify new session added
        Assert.Equal(2, repo.Sessions.Count);
        var newSession = repo.Sessions[1];
        Assert.Equal(user.Id, newSession.UserId);
        Assert.Null(newSession.RevokedAt);
    }

    [Fact]
    public async Task Refresh_WithAlreadyRevokedToken_ShouldTriggerReuseDetectionAndRevokeAll()
    {
        var repo = new FakeAuthRepository();
        var tokens = new FakeTokenService();
        var clock = new FakeDateTimeProvider();

        var user = new User("tester", "tester@test.com", "hash", UserRole.User, false, clock.UtcNow) { Id = 42 };
        repo.Users.Add(user);

        // Already revoked session
        var reusedToken = "refresh_token_reused";
        var revokedSession = new RefreshSession(
            userId: user.Id,
            tokenHash: tokens.HashRefreshToken(reusedToken),
            jwtId: "jwt1",
            now: clock.UtcNow.AddHours(-2),
            expiresAt: clock.UtcNow.AddDays(7),
            ip: "127.0.0.1",
            userAgent: "Mozilla"
        );
        revokedSession.Revoke(clock.UtcNow.AddHours(-1), "127.0.0.1", "rotated");
        repo.Sessions.Add(revokedSession);

        // Active session that should be revoked due to reuse detection
        var otherActiveSession = new RefreshSession(
            userId: user.Id,
            tokenHash: tokens.HashRefreshToken("other_active"),
            jwtId: "jwt2",
            now: clock.UtcNow.AddHours(-1),
            expiresAt: clock.UtcNow.AddDays(7),
            ip: "127.0.0.1",
            userAgent: "Mozilla"
        );
        repo.Sessions.Add(otherActiveSession);

        var handler = new RefreshHandler(repo, tokens, clock);
        var command = new RefreshCommand(reusedToken, "127.0.0.1", "Mozilla");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(command, CancellationToken.None)
        );

        Assert.Equal("REFRESH_TOKEN_REUSED", exception.Code);

        // Verify reuse detection triggered and ALL sessions are revoked
        Assert.Contains("42:refresh token reuse", repo.RevokedUserSessionsReasons);
        Assert.NotNull(otherActiveSession.RevokedAt);
        Assert.Equal("refresh token reuse", otherActiveSession.RevokeReason);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ShouldThrowExpiredException()
    {
        var repo = new FakeAuthRepository();
        var tokens = new FakeTokenService();
        var clock = new FakeDateTimeProvider();

        var user = new User("tester", "tester@test.com", "hash", UserRole.User, false, clock.UtcNow) { Id = 42 };
        repo.Users.Add(user);

        // Expired session
        var expiredToken = "refresh_token_expired";
        var expiredSession = new RefreshSession(
            userId: user.Id,
            tokenHash: tokens.HashRefreshToken(expiredToken),
            jwtId: "jwt1",
            now: clock.UtcNow.AddDays(-8),
            expiresAt: clock.UtcNow.AddDays(-1),
            ip: "127.0.0.1",
            userAgent: "Mozilla"
        );
        repo.Sessions.Add(expiredSession);

        var handler = new RefreshHandler(repo, tokens, clock);
        var command = new RefreshCommand(expiredToken, "127.0.0.1", "Mozilla");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(command, CancellationToken.None)
        );

        Assert.Equal("REFRESH_TOKEN_EXPIRED", exception.Code);
    }
}
