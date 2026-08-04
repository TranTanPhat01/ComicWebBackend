using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ComicWeb.WebApi.OpenApi;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var anonymous = context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any()
            || context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any() == true;
        var authorized = context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any()
            || context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true;
        if (anonymous || !authorized) return;
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() });
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized ProblemDetails" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden ProblemDetails" });
    }
}
