using ComicWeb.Application.Features.Genres;
using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace ComicWeb.Persistence.Content;

public sealed class GenreSeedService(IServiceScopeFactory scopeFactory, ILogger<GenreSeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextOptions<ComicWeb.Persistence.Contexts.ApplicationDbContext>>();
            await using var context = new ComicWeb.Persistence.Contexts.ApplicationDbContext(db);

            var existingSlugs = await context.Genres.AsNoTracking().Select(x => x.Slug.ToLower()).ToListAsync(cancellationToken);
            var desired = DefaultGenreCatalog.Items
                .Where(x => !existingSlugs.Contains(x.Slug.ToLower()))
                .Select(x => new Genre { Name = x.Name, Slug = x.Slug, Description = x.Description, IsActive = true })
                .ToList();

            if (desired.Count == 0)
            {
                return;
            }

            context.Genres.AddRange(desired);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} genres", desired.Count);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(ex, "Skipping genre seeding because the database is unavailable.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
