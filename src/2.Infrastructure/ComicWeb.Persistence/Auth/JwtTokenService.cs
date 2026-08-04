using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace ComicWeb.Persistence.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    public AccessToken CreateAccessToken(User user, Guid sessionId, DateTime now) { var exp = now.AddMinutes(_options.AccessTokenMinutes); var jti = Guid.NewGuid().ToString("N"); var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Username), new Claim(ClaimTypes.Role, user.Role.ToString()), new Claim(JwtRegisteredClaimNames.Jti, jti), new Claim("session_id", sessionId.ToString()), new Claim("password_changed", (!user.MustChangePassword).ToString()) }; var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, notBefore: now, expires: exp, signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256)); return new(new JwtSecurityTokenHandler().WriteToken(token), jti, exp); }
    public string CreateRefreshToken() { var b = RandomNumberGenerator.GetBytes(64); return Convert.ToBase64String(b); }
    public string HashRefreshToken(string t) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(t)));
}
