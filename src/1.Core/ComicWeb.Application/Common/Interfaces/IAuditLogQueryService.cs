using ComicWeb.Application.Features.Stories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Interfaces;

public sealed record AuditLogDto(
    long Id,
    int? ActorUserId,
    string? ActorUsername,
    string ActorType,
    string Action,
    string EntityType,
    string? EntityId,
    string Result,
    DateTime OccurredAt,
    string? RequestId,
    string? IpAddress,
    string? UserAgent,
    string? DetailsJson,
    string? ErrorCode);

public sealed record AuditLogQuery(
    int Page = 1,
    int PageSize = 20,
    int? ActorUserId = null,
    string? Action = null,
    string? EntityType = null,
    string? EntityId = null,
    string? Result = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

public interface IAuditLogQueryService
{
    Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}
