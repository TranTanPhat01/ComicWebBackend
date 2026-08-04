using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ComicWeb.WebApi.Infrastructure;

public static class ProblemResponse
{
    public static Task WriteAsync(HttpContext context, int status, string code, string title, string detail)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails { Type = $"https://comicweb/errors/{code.ToLowerInvariant()}", Title = title, Status = status, Detail = detail };
        problem.Extensions["code"] = code;
        problem.Extensions["requestId"] = context.TraceIdentifier;
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
