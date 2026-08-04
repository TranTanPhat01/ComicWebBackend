using ComicWeb.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

namespace ComicWeb.WebApi.Middlewares;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception. RequestId: {RequestId}",
                context.TraceIdentifier);

            var appException = ToAppException(exception);
            var problem = new ProblemDetails
            {
                Type = $"https://comicweb/errors/{appException.Code.ToLowerInvariant()}",
                Title = appException.Title,
                Status = appException.StatusCode,
                Detail = appException.Message
            };

            problem.Extensions["code"] = appException.Code;
            problem.Extensions["requestId"] = context.TraceIdentifier;

            context.Response.StatusCode = problem.Status.Value;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    problem,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
    }

    private static AppException ToAppException(Exception exception)
    {
        if (exception is AppException appException)
        {
            return appException;
        }

        if (exception is DbUpdateException
            {
                InnerException: PostgresException postgresException
            }
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new AppException(
                "DUPLICATE_RESOURCE",
                StatusCodes.Status409Conflict,
                "Conflict",
                "A record with the same unique value already exists.");
        }

        return new AppException(
            "INTERNAL_ERROR",
            StatusCodes.Status500InternalServerError,
            "Internal server error",
            "An unexpected server error occurred.");
    }
}
