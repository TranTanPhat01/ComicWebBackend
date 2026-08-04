namespace ComicWeb.Domain.Entities;

public class RefreshSession
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public int UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string JwtId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? UserAgent { get; private set; }
    public string? RevokeReason { get; private set; }
    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
    private RefreshSession() { }
    public RefreshSession(int userId, string tokenHash, string jwtId, DateTime now, DateTime expiresAt, string? ip, string? userAgent)
    { UserId = userId; TokenHash = tokenHash; JwtId = jwtId; CreatedAt = now; ExpiresAt = expiresAt; CreatedByIp = ip; UserAgent = userAgent; }
    public void Revoke(DateTime now, string? ip, string reason, Guid? replacedBy = null)
    { if (RevokedAt is not null) return; RevokedAt = now; RevokedByIp = ip; RevokeReason = reason; ReplacedBySessionId = replacedBy; }
}
