using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Stories;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Persistence.Logging;

public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private readonly IApplicationDbContext _db;

    public AuditLogQueryService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var q = _db.AuditLogs.AsNoTracking();

        if (query.ActorUserId.HasValue)
        {
            q = q.Where(x => x.ActorUserId == query.ActorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            q = q.Where(x => x.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            q = q.Where(x => x.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            q = q.Where(x => x.EntityId == query.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(query.Result))
        {
            q = q.Where(x => x.Result == query.Result);
        }

        if (query.FromUtc.HasValue)
        {
            q = q.Where(x => x.OccurredAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            q = q.Where(x => x.OccurredAt <= query.ToUtc.Value);
        }

        // Sort newest first
        q = q.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id);

        var totalItems = await q.CountAsync(cancellationToken);
        
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize > 100 ? 100 : query.PageSize;

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogDto(
                x.Id,
                x.ActorUserId,
                x.ActorUsername,
                x.ActorType,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.Result,
                x.OccurredAt,
                x.RequestId,
                x.IpAddress,
                x.UserAgent,
                x.DetailsJson,
                x.ErrorCode
            ))
            .ToListAsync(cancellationToken);

        var totalPages = (totalItems + pageSize - 1) / pageSize;

        return new PagedResult<AuditLogDto>(items, new PageMeta(page, pageSize, totalItems, totalPages));
    }
}
