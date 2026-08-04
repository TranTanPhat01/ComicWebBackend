using System;

namespace ComicWeb.Application.Common.Interfaces;

public sealed record AuditActorContext(
    int? UserId,
    string? Username,
    string ActorType,
    string? RequestId,
    string? IpAddress,
    string? UserAgent);

public interface IAuditContextAccessor
{
    AuditActorContext GetCurrent();
    IDisposable UseSystemContext();
}
