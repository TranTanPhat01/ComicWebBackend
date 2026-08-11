namespace ComicWeb.Application.Common.Interfaces;

/// <summary>
/// Thread-safe view count service.
/// Uses Redis INCR when available, falls back to in-memory dictionary.
/// Periodic flush writes accumulated counts to PostgreSQL.
/// </summary>
public interface IViewCountService
{
    /// <summary>Increment the view count for a story (fire-and-forget safe).</summary>
    Task IncrementAsync(int storyId, CancellationToken ct = default);

    /// <summary>Flush all pending counts to the database and reset the buffer.</summary>
    Task<int> FlushAsync(CancellationToken ct = default);
}
