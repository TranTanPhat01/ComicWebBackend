using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ComicWeb.Domain.Enums;
using Microsoft.IdentityModel.Tokens;

namespace ComicWeb.WebApi.IntegrationTests;

public static class JwtTestTokenFactory
{
    public const string Issuer = "ComicWebIntegrationTests";
    public const string Audience = "ComicWebIntegrationTestsClient";
    public const string SigningKey = "postgresql-test-signing-key-at-least-32-characters-long";

    public static string Create(
        int userId,
        UserRole role,
        bool passwordChanged,
        DateTime? expiresAt = null,
        bool useInvalidSigningKey = false)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "integration-user"),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("password_changed", passwordChanged.ToString())
        };

        var signingKey = useInvalidSigningKey
            ? "invalid-signing-key-for-integration-tests-only"
            : SigningKey;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var expiration = expiresAt ?? now.AddMinutes(5);
        var notBefore = expiration <= now
            ? expiration.AddMinutes(-1)
            : now.AddMinutes(-1);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            notBefore: notBefore,
            expires: expiration,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
