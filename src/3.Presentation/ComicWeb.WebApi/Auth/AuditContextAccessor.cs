using ComicWeb.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading;

namespace ComicWeb.WebApi.Auth;

public sealed class AuditContextAccessor : IAuditContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly AsyncLocal<AuditActorContext?> _currentOverride = new();

    public AuditContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public AuditActorContext GetCurrent()
    {
        // 1. Check if there is a background override context (e.g. System actor)
        if (_currentOverride.Value != null)
        {
            return _currentOverride.Value;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return new AuditActorContext(null, null, "Anonymous", null, null, null);
        }

        var user = httpContext.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;

        int? userId = null;
        string? username = null;
        string actorType = "Anonymous";

        if (isAuthenticated && user is not null)
        {
            actorType = "User";
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idClaim, out var id))
            {
                userId = id;
            }
            username = user.FindFirst(ClaimTypes.Name)?.Value;
        }

        var requestId = httpContext.TraceIdentifier;
        var ip = httpContext.Connection?.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request?.Headers["User-Agent"].ToString();

        return new AuditActorContext(userId, username, actorType, requestId, ip, userAgent);
    }

    public IDisposable UseSystemContext()
    {
        var prev = _currentOverride.Value;
        _currentOverride.Value = new AuditActorContext(null, "system", "System", null, null, null);
        return new SystemContextScope(prev);
    }

    private sealed class SystemContextScope : IDisposable
    {
        private readonly AuditActorContext? _previous;

        public SystemContextScope(AuditActorContext? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            _currentOverride.Value = _previous;
        }
    }
}
