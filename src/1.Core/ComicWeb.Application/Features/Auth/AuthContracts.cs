using ComicWeb.Domain.Enums;
namespace ComicWeb.Application.Features.Auth;

public sealed record AuthUserDto(int Id, string Username, string Email, UserRole Role);
public sealed record LoginResponse(string AccessToken, int ExpiresIn, bool MustChangePassword, AuthUserDto User);
public sealed record LoginOperationResult(LoginResponse Response, string RefreshToken, DateTime RefreshExpiresAt, Guid SessionId);
public sealed record RefreshOperationResult(string AccessToken, int ExpiresIn, string RefreshToken, DateTime RefreshExpiresAt);
public sealed record MeResponse(int Id, string Username, string Email, UserRole Role, bool MustChangePassword, IReadOnlyList<string> Permissions);
