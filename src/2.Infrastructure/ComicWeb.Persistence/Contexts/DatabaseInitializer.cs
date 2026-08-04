using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ComicWeb.Persistence.Contexts;

public static class DatabaseInitializer
{
    public static async Task MigrateDatabaseAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var database = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<DatabaseInitializationOptions>>()
            .Value;
        var connection = database.Database.GetDbConnection();

        for (var attempt = 1; attempt <= options.MaxRetryAttempts; attempt++)
        {
            try
            {
                logger.LogInformation(
                    "Applying pending database migrations to database {Database} on server {Server}. Attempt {Attempt}/{MaxAttempts}.",
                    connection.Database,
                    connection.DataSource,
                    attempt,
                    options.MaxRetryAttempts);

                await database.Database.MigrateAsync(cancellationToken);

                logger.LogInformation(
                    "Database migrations completed for database {Database} on server {Server}.",
                    connection.Database,
                    connection.DataSource);
                return;
            }
            catch (Exception exception) when (
                IsTransientConnectionFailure(exception)
                && attempt < options.MaxRetryAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Database is not ready. Retrying migration attempt {NextAttempt}/{MaxAttempts} in {DelaySeconds} seconds.",
                    attempt + 1,
                    options.MaxRetryAttempts,
                    options.RetryDelaySeconds);

                await Task.Delay(
                    TimeSpan.FromSeconds(options.RetryDelaySeconds),
                    cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "Database migration did not complete within the configured retry attempts.");
    }

    internal static bool IsTransientConnectionFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException)
            {
                return false;
            }

            if (current is SocketException or TimeoutException or NpgsqlException)
            {
                return true;
            }
        }

        return false;
    }
}
