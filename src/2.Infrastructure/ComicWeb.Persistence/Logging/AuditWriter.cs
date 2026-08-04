using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Persistence.Logging;

public sealed class AuditWriter : IAuditWriter
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditContextAccessor _contextAccessor;
    private readonly IAuditDetailsSerializer _serializer;
    private readonly IDateTimeProvider _dateTime;
    private readonly IServiceProvider _serviceProvider;

    public AuditWriter(
        IApplicationDbContext db,
        IAuditContextAccessor contextAccessor,
        IAuditDetailsSerializer serializer,
        IDateTimeProvider dateTime,
        IServiceProvider serviceProvider)
    {
        _db = db;
        _contextAccessor = contextAccessor;
        _serializer = serializer;
        _dateTime = dateTime;
        _serviceProvider = serviceProvider;
    }

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var context = _contextAccessor.GetCurrent();
        
        var actorType = auditEvent.ActorType ?? context.ActorType;
        var detailsJson = _serializer.Serialize(auditEvent.Details);

        var log = new AuditLog(
            actorUserId: actorType == "System" ? null : (auditEvent.ActorUserId ?? context.UserId),
            actorUsername: actorType == "System" ? "system" : (auditEvent.ActorUsername ?? context.Username),
            actorType: actorType,
            action: auditEvent.Action,
            entityType: auditEvent.EntityType,
            entityId: auditEvent.EntityId,
            result: auditEvent.Result,
            occurredAt: _dateTime.UtcNow,
            requestId: context.RequestId,
            ipAddress: context.IpAddress,
            userAgent: context.UserAgent,
            detailsJson: detailsJson,
            errorCode: auditEvent.ErrorCode
        );

        if (auditEvent.Result == "Success")
        {
            // Strict transaction strategy:
            // Success logs append to current tracker to commit in the same transaction
            _db.AuditLogs.Add(log);
        }
        else
        {
            // Failed/Denied logs are saved immediately using a fresh isolated DB connection/context
            // to avoid saving dirty or failing states of the current DbContext tracker.
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var freshDb = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                freshDb.AuditLogs.Add(log);
                await freshDb.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
                // Best-effort for failed/denied logging.
                // We do not crash the request if writing a failed/denied log itself fails (e.g. database unconfigured in unit tests).
            }
        }
    }
}
