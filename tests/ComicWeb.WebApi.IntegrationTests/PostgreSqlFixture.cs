using ComicWeb.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ComicWeb.WebApi.IntegrationTests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("comicweb_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var database = CreateContext();
        await database.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task ResetAsync()
    {
        await using var database = CreateContext();
        await database.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"RefreshSessions\", \"Chapters\", \"Stories\", \"Users\", \"SystemLogs\", \"UserNotifications\", \"AuditLogs\" RESTART IDENTITY CASCADE;");
    }
}

[CollectionDefinition("PostgreSql")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}
