using ComicWeb.Domain.Common;
using ComicWeb.Domain.Enums;

namespace ComicWeb.Domain.Entities;

public class Story : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CoverImageUrl { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public StoryStatus Status { get; set; } = StoryStatus.Draft;
    public DateTime? PublishedAt { get; private set; }
    public DateTime? ScheduledAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public int Version { get; private set; }
    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    public ICollection<Genre> Genres { get; set; } = new List<Genre>();

    public void UpdateDetails(string title, string slug, string description, string? coverUrl, string? authorName, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(slug)) throw new InvalidOperationException("Title and slug are required.");
        Title = title.Trim(); Slug = slug.Trim(); Description = description?.Trim() ?? string.Empty; CoverImageUrl = coverUrl?.Trim() ?? string.Empty; AuthorName = authorName?.Trim(); UpdateAt = now; Version++;
    }

    public void Publish(DateTime now) { EnsureActive(); Status = StoryStatus.Published; PublishedAt ??= now; ScheduledAt = null; UpdateAt = now; Version++; }
    public void Schedule(DateTime? scheduledAt, DateTime now)
    {
        EnsureActive();
        ScheduledAt = scheduledAt;
        UpdateAt = now;
        Version++;
    }
    public void Unpublish(DateTime now) { EnsureActive(); if (Status != StoryStatus.Published) throw new InvalidOperationException("Invalid status transition."); Status = StoryStatus.Draft; ScheduledAt = null; UpdateAt = now; Version++; }
    public void Hide(DateTime now) { EnsureActive(); if (Status != StoryStatus.Published) throw new InvalidOperationException("Invalid status transition."); Status = StoryStatus.Hidden; UpdateAt = now; Version++; }
    public void Complete(DateTime now) { EnsureActive(); if (Status != StoryStatus.Published) throw new InvalidOperationException("Invalid status transition."); Status = StoryStatus.Completed; UpdateAt = now; Version++; }
    public void SoftDelete(DateTime now) { DeletedAt = now; UpdateAt = now; Version++; }
    public void Restore(DateTime now) { DeletedAt = null; UpdateAt = now; Version++; }
    private void EnsureActive() { if (DeletedAt is not null) throw new InvalidOperationException("Story is deleted."); if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Slug)) throw new InvalidOperationException("Story is not publishable."); }
}
