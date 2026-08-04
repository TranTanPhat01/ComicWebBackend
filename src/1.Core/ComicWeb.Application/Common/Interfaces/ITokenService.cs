using ComicWeb.Domain.Entities;
namespace ComicWeb.Application.Common.Interfaces;

public interface ITokenService
{
    AccessToken CreateAccessToken(User user, Guid sessionId, DateTime now);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
}
public sealed record AccessToken(string Value, string JwtId, DateTime ExpiresAt);
