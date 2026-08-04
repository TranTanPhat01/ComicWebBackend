using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Interfaces;

public sealed record AuditEvent(
    string Action,
    string EntityType,
    string? EntityId,
    string Result,
    object? Details = null,
    string? ErrorCode = null,
    string? ActorType = null,
    int? ActorUserId = null,
    string? ActorUsername = null);

public interface IAuditWriter
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
