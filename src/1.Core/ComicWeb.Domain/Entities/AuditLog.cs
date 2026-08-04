using System;

namespace ComicWeb.Domain.Entities;

public sealed class AuditLog
{
    public long Id { get; private set; }

    public int? ActorUserId { get; private set; }

    public string? ActorUsername { get; private set; }

    public string ActorType { get; private set; } = null!;

    public string Action { get; private set; } = null!;

    public string EntityType { get; private set; } = null!;

    public string? EntityId { get; private set; }

    public string Result { get; private set; } = null!;

    public DateTime OccurredAt { get; private set; }

    public string? RequestId { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string? DetailsJson { get; private set; }

    public string? ErrorCode { get; private set; }

    // EF Constructor
    private AuditLog() { }

    public AuditLog(
        int? actorUserId,
        string? actorUsername,
        string actorType,
        string action,
        string entityType,
        string? entityId,
        string result,
        DateTime occurredAt,
        string? requestId,
        string? ipAddress,
        string? userAgent,
        string? detailsJson,
        string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(actorType)) throw new ArgumentException("ActorType is required.", nameof(actorType));
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("Action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("EntityType is required.", nameof(entityType));
        if (string.IsNullOrWhiteSpace(result)) throw new ArgumentException("Result is required.", nameof(result));
        if (occurredAt.Kind != DateTimeKind.Utc) throw new ArgumentException("OccurredAt must be UTC.", nameof(occurredAt));

        ActorUserId = actorUserId;
        ActorUsername = actorUsername?.Trim();
        ActorType = actorType.Trim();
        Action = action.Trim();
        EntityType = entityType.Trim();
        EntityId = entityId?.Trim();
        Result = result.Trim();
        OccurredAt = occurredAt;
        RequestId = requestId?.Trim();
        IpAddress = ipAddress?.Trim();
        UserAgent = userAgent?.Trim();
        DetailsJson = detailsJson;
        ErrorCode = errorCode?.Trim();
    }
}
