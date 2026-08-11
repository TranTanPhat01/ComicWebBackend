using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using MediatR;

namespace ComicWeb.Application.Features.Auth;

public sealed record LoginCommand(string UsernameOrEmail, string Password, string? IpAddress, string? UserAgent) : IRequest<LoginOperationResult>;
public sealed record RefreshCommand(string RefreshToken, string? IpAddress, string? UserAgent) : IRequest<RefreshOperationResult>;
public sealed record LogoutCommand(string? RefreshToken, string? IpAddress) : IRequest;
public sealed record ChangePasswordCommand(int UserId, string CurrentPassword, string NewPassword, string ConfirmPassword, string? IpAddress) : IRequest;
public sealed record GetMeQuery(int UserId) : IRequest<MeResponse>;

public sealed class LoginHandler(IAuthRepository repository, IPasswordHasher passwords, ITokenService tokens, IDateTimeProvider clock, IAuthenticationSecurityPolicy securityPolicy, IAuditWriter auditWriter) : IRequestHandler<LoginCommand, LoginOperationResult>
{
    public async Task<LoginOperationResult> Handle(LoginCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow; var normalized = request.UsernameOrEmail.Trim().ToUpperInvariant();
        var user = await repository.FindByUsernameOrEmailAsync(normalized, ct);
        
        if (user is not null && user.IsLockedOut(now))
        {
            await auditWriter.WriteAsync(new AuditEvent(
                Action: "ACCOUNT_LOCKED",
                EntityType: "User",
                EntityId: user.Id.ToString(),
                Result: "Failed",
                Details: new { username = user.Username },
                ErrorCode: "ACCOUNT_LOCKED",
                ActorType: "User",
                ActorUserId: user.Id,
                ActorUsername: user.Username
            ), ct);
            throw new AppException("ACCOUNT_LOCKED", 423, "Account locked", "Tài khoản đang tạm thời bị khóa.");
        }
        
        if (user is null || !passwords.Verify(request.Password, user.PasswordHash))
        {
            if (user is not null) { user.RecordFailedLogin(now, securityPolicy.MaxFailedLoginAttempts, securityPolicy.LockoutDuration); await repository.SaveChangesAsync(ct); }
            
            await auditWriter.WriteAsync(new AuditEvent(
                Action: "LOGIN_FAILED",
                EntityType: "User",
                EntityId: user?.Id.ToString(),
                Result: "Failed",
                Details: new { usernameOrEmail = Mask(request.UsernameOrEmail) },
                ErrorCode: "INVALID_CREDENTIALS",
                ActorType: user is null ? "Anonymous" : "User",
                ActorUserId: user?.Id,
                ActorUsername: user?.Username
            ), ct);
            
            throw new AppException("INVALID_CREDENTIALS", 401, "Authentication failed", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }
        
        if (!user.IsActive)
        {
            await auditWriter.WriteAsync(new AuditEvent(
                Action: "LOGIN_FAILED",
                EntityType: "User",
                EntityId: user.Id.ToString(),
                Result: "Failed",
                Details: new { usernameOrEmail = user.Username },
                ErrorCode: "ACCOUNT_INACTIVE",
                ActorType: "User",
                ActorUserId: user.Id,
                ActorUsername: user.Username
            ), ct);
            throw new AppException("ACCOUNT_INACTIVE", 401, "Authentication failed", "Tài khoản không hoạt động.");
        }
        
        user.RecordSuccessfulLogin(now);
        var rawRefresh = tokens.CreateRefreshToken();
        var session = new RefreshSession(user.Id, tokens.HashRefreshToken(rawRefresh), Guid.NewGuid().ToString("N"), now, now.AddDays(7), request.IpAddress, request.UserAgent);
        await repository.AddSessionAsync(session, ct);
        
        await auditWriter.WriteAsync(new AuditEvent(
            Action: "LOGIN_SUCCEEDED",
            EntityType: "User",
            EntityId: user.Id.ToString(),
            Result: "Success",
            Details: new { username = user.Username },
            ActorType: "User",
            ActorUserId: user.Id,
            ActorUsername: user.Username
        ), ct);

        await repository.SaveChangesAsync(ct);
        
        var access = tokens.CreateAccessToken(user, session.Id, now);
        return new(new(access.Value, (int)(access.ExpiresAt - now).TotalSeconds, user.MustChangePassword, new(user.Id, user.Username, user.Email, user.Role)), rawRefresh, session.ExpiresAt, session.Id);
    }

    private static string Mask(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        if (input.Contains("@"))
        {
            var parts = input.Split('@');
            if (parts[0].Length <= 2) return $"{parts[0][0]}***@{parts[1]}";
            return $"{parts[0][0]}***{parts[0][^1]}@{parts[1]}";
        }
        if (input.Length <= 2) return "***";
        return $"{input[0]}***{input[^1]}";
    }
}
public sealed class RefreshHandler(IAuthRepository repository, ITokenService tokens, IDateTimeProvider clock) : IRequestHandler<RefreshCommand, RefreshOperationResult>
{
    public async Task<RefreshOperationResult> Handle(RefreshCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow; var session = await repository.FindSessionByTokenHashAsync(tokens.HashRefreshToken(request.RefreshToken), ct);
        if (session is null) throw new AppException("REFRESH_TOKEN_INVALID", 401, "Authentication failed", "Phiên đăng nhập không hợp lệ.");
        if (session.RevokedAt is not null) { await repository.RevokeAllSessionsAsync(session.UserId, now, request.IpAddress, "refresh token reuse", ct); await repository.SaveChangesAsync(ct); throw new AppException("REFRESH_TOKEN_REUSED", 401, "Authentication failed", "Phiên đăng nhập không hợp lệ."); }
        if (session.ExpiresAt <= now) throw new AppException("REFRESH_TOKEN_EXPIRED", 401, "Authentication failed", "Phiên đăng nhập đã hết hạn.");
        var user = await repository.FindByIdAsync(session.UserId, ct); if (user is null || !user.IsActive) throw new AppException("ACCOUNT_INACTIVE", 401, "Authentication failed", "Tài khoản không hoạt động.");
        var raw = tokens.CreateRefreshToken(); var next = new RefreshSession(user.Id, tokens.HashRefreshToken(raw), Guid.NewGuid().ToString("N"), now, now.AddDays(7), request.IpAddress, request.UserAgent);
        session.Revoke(now, request.IpAddress, "rotated", next.Id); await repository.AddSessionAsync(next, ct); await repository.SaveChangesAsync(ct);
        var access = tokens.CreateAccessToken(user, next.Id, now); return new(access.Value, (int)(access.ExpiresAt - now).TotalSeconds, raw, next.ExpiresAt);
    }
}
public sealed class LogoutHandler(IAuthRepository repository, ITokenService tokens, IDateTimeProvider clock) : IRequestHandler<LogoutCommand>
{ public async Task Handle(LogoutCommand r, CancellationToken ct) { if (string.IsNullOrWhiteSpace(r.RefreshToken)) return; var s = await repository.FindSessionByTokenHashAsync(tokens.HashRefreshToken(r.RefreshToken), ct); if (s is not null) { s.Revoke(clock.UtcNow, r.IpAddress, "logout"); await repository.SaveChangesAsync(ct); } } }
public sealed class ChangePasswordHandler(IAuthRepository repository, IPasswordHasher passwords, IDateTimeProvider clock, IAuditWriter auditWriter) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand r, CancellationToken ct)
    {
        if (r.NewPassword != r.ConfirmPassword) throw new AppException("VALIDATION_ERROR", 400, "Validation failed", "Xác nhận mật khẩu không khớp.");
        
        var u = await repository.FindByIdAsync(r.UserId, ct) ?? throw new AppException("UNAUTHORIZED", 401, "Unauthorized", "Không xác thực được người dùng.");
        
        if (!passwords.Verify(r.CurrentPassword, u.PasswordHash))
        {
            await auditWriter.WriteAsync(new AuditEvent(
                Action: "PASSWORD_CHANGED",
                EntityType: "User",
                EntityId: u.Id.ToString(),
                Result: "Failed",
                Details: new { username = u.Username },
                ErrorCode: "CURRENT_PASSWORD_INVALID",
                ActorType: "User",
                ActorUserId: u.Id,
                ActorUsername: u.Username
            ), ct);
            throw new AppException("CURRENT_PASSWORD_INVALID", 400, "Validation failed", "Mật khẩu hiện tại không chính xác.");
        }
        
        if (passwords.Verify(r.NewPassword, u.PasswordHash))
        {
            await auditWriter.WriteAsync(new AuditEvent(
                Action: "PASSWORD_CHANGED",
                EntityType: "User",
                EntityId: u.Id.ToString(),
                Result: "Failed",
                Details: new { username = u.Username },
                ErrorCode: "PASSWORD_POLICY_FAILED",
                ActorType: "User",
                ActorUserId: u.Id,
                ActorUsername: u.Username
            ), ct);
            throw new AppException("PASSWORD_POLICY_FAILED", 400, "Password policy failed", "Mật khẩu mới phải khác mật khẩu hiện tại.");
        }

        PasswordPolicy.EnsureValid(r.NewPassword, u.Username, u.Email);
        u.ChangePassword(passwords.Hash(r.NewPassword), clock.UtcNow);
        await repository.RevokeAllSessionsAsync(u.Id, clock.UtcNow, r.IpAddress, "password changed", ct);

        await auditWriter.WriteAsync(new AuditEvent(
            Action: "PASSWORD_CHANGED",
            EntityType: "User",
            EntityId: u.Id.ToString(),
            Result: "Success",
            Details: new { username = u.Username },
            ActorType: "User",
            ActorUserId: u.Id,
            ActorUsername: u.Username
        ), ct);

        await repository.SaveChangesAsync(ct);
    }
}
public sealed class GetMeHandler(IAuthRepository repository) : IRequestHandler<GetMeQuery, MeResponse>
{ public async Task<MeResponse> Handle(GetMeQuery r, CancellationToken ct) { var u = await repository.FindByIdAsync(r.UserId, ct); if (u is null || !u.IsActive) throw new AppException("UNAUTHORIZED", 401, "Unauthorized", "Không xác thực được người dùng."); var p = u.Role == UserRole.Admin ? new[] { "admin.access", "stories.read", "stories.create", "stories.update", "stories.delete", "stories.publish", "chapters.manage" } : Array.Empty<string>(); return new(u.Id, u.Username, u.Email, u.Role, u.MustChangePassword, p); } }
