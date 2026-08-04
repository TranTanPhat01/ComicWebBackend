using ComicWeb.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection;

namespace ComicWeb.WebApi.Infrastructure;

public sealed class ProblemDetailsAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var auditWriter = context.RequestServices.GetRequiredService<IAuditWriter>();

        if (!(context.User.Identity?.IsAuthenticated ?? false))
        {
            await auditWriter.WriteAsync(new AuditEvent(
                Action: "ADMIN_ACCESS_UNAUTHORIZED",
                EntityType: "Security",
                EntityId: null,
                Result: "Denied",
                Details: new { path = context.Request.Path.Value, method = context.Request.Method },
                ErrorCode: "UNAUTHORIZED"
            ));
            await ProblemResponse.WriteAsync(context, 401, "UNAUTHORIZED", "Unauthorized", "Authentication is required.");
            return;
        }

        if (context.User.HasClaim("password_changed", "False"))
        {
            await auditWriter.WriteAsync(new AuditEvent(
                Action: "ADMIN_ACCESS_DENIED",
                EntityType: "Security",
                EntityId: null,
                Result: "Denied",
                Details: new { path = context.Request.Path.Value, method = context.Request.Method },
                ErrorCode: "PASSWORD_CHANGE_REQUIRED"
            ));
            await ProblemResponse.WriteAsync(context, 403, "PASSWORD_CHANGE_REQUIRED", "Password change required", "You must change your password before accessing this resource.");
            return;
        }

        await auditWriter.WriteAsync(new AuditEvent(
            Action: "ADMIN_ACCESS_DENIED",
            EntityType: "Security",
            EntityId: null,
            Result: "Denied",
            Details: new { path = context.Request.Path.Value, method = context.Request.Method },
            ErrorCode: "FORBIDDEN"
        ));
        await ProblemResponse.WriteAsync(context, 403, "FORBIDDEN", "Forbidden", "You do not have permission to access this resource.");
    }
}
