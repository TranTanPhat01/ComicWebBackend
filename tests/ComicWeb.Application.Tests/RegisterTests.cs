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

public class RegisterTests
{
    private class FakeAuthRepository : IAuthRepository
    {
        public List<User> Users { get; } = new();

        public Task<User?> FindByUsernameOrEmailAsync(string normalizedValue, CancellationToken ct)
        {
            var u = Users.Find(x => x.NormalizedUsername == normalizedValue || x.NormalizedEmail == normalizedValue);
            return Task.FromResult(u);
        }

        public Task<User?> FindByIdAsync(int id, CancellationToken ct)
        {
            var u = Users.Find(x => x.Id == id);
            return Task.FromResult(u);
        }

        public Task<bool> HasAdministratorAsync(CancellationToken ct) => Task.FromResult(false);

        public Task AddUserAsync(User user, CancellationToken ct)
        {
            user.Id = Users.Count + 1;
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task<RefreshSession?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken ct) => Task.FromResult<RefreshSession?>(null);
        public Task AddSessionAsync(RefreshSession session, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAllSessionsAsync(int userId, DateTime now, string? ip, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string pwd) => "hashed_" + pwd;
        public bool Verify(string pwd, string hash) => hash == "hashed_" + pwd;
    }

    private class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);
    }

    private class FakeAuditWriter : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = new();
        public Task WriteAsync(AuditEvent ev, CancellationToken ct)
        {
            Events.Add(ev);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Register_WithValidData_ShouldCreateUser()
    {
        var repo = new FakeAuthRepository();
        var handler = new RegisterHandler(
            repo,
            new FakePasswordHasher(),
            new FakeDateTimeProvider(),
            new FakeAuditWriter()
        );

        var result = await handler.Handle(new RegisterCommand("newuser", "newuser@example.test", "StrongPass2026!"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("newuser", result.Username);
        Assert.Equal("newuser@example.test", result.Email);
        Assert.Equal(UserRole.User, result.Role);
        Assert.Single(repo.Users);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ShouldThrowException()
    {
        var repo = new FakeAuthRepository();
        var now = DateTime.UtcNow;
        var existing = new User("existinguser", "existing@example.test", "hash", UserRole.User, false, now);
        await repo.AddUserAsync(existing, CancellationToken.None);

        var handler = new RegisterHandler(
            repo,
            new FakePasswordHasher(),
            new FakeDateTimeProvider(),
            new FakeAuditWriter()
        );

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(new RegisterCommand("existinguser", "different@example.test", "StrongPass2026!"), CancellationToken.None)
        );

        Assert.Equal("USERNAME_TAKEN", exception.Code);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ShouldThrowException()
    {
        var repo = new FakeAuthRepository();
        var handler = new RegisterHandler(
            repo,
            new FakePasswordHasher(),
            new FakeDateTimeProvider(),
            new FakeAuditWriter()
        );

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(new RegisterCommand("newuser", "newuser@example.test", "123456"), CancellationToken.None)
        );

        Assert.Equal("PASSWORD_POLICY_FAILED", exception.Code);
    }
}
