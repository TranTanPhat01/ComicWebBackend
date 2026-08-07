using System.Collections.Concurrent;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Common.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ComicWeb.Persistence.Content;

/// <summary>
/// In-memory view count buffer with periodic flush to PostgreSQL.
/// Thread-safe via ConcurrentDictionary. Singleton lifetime — uses IServiceScopeFactory for DB access.
/// </summary>
public sealed class InMemoryViewCountService(
    IServiceScopeFactory scopeFactory,
    ILogger<InMemoryViewCountService> logger)
    : IViewCountService
{
    private readonly ConcurrentDictionary<int, int> _buffer = new();

    public Task IncrementAsync(int storyId, CancellationToken ct = default)
    {
        _buffer.AddOrUpdate(storyId, 1, (_, existing) => existing + 1);
        return Task.CompletedTask;
    }

    public async Task<int> FlushAsync(CancellationToken ct = default)
    {
        if (_buffer.IsEmpty) return 0;

        // Atomically drain the buffer
        var snapshot = new List<(int StoryId, int Count)>();
        foreach (var key in _buffer.Keys.ToList())
        {
            if (_buffer.TryRemove(key, out var count))
                snapshot.Add((key, count));
        }

        if (snapshot.Count == 0) return 0;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            foreach (var (storyId, count) in snapshot)
            {
                await db.Stories
                    .Where(s => s.Id == storyId)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(x => x.ViewCount, x => x.ViewCount + count),
                        ct);
            }

            logger.LogInformation("Flushed view counts for {Count} stories.", snapshot.Count);
            return snapshot.Count;
        }
        catch (Exception ex)
        {
            // Put the counts back into the buffer to avoid losing them
            foreach (var (storyId, count) in snapshot)
                _buffer.AddOrUpdate(storyId, count, (_, existing) => existing + count);

            logger.LogError(ex, "Failed to flush view counts. Counts re-queued for next cycle.");
            return 0;
        }
    }
}
