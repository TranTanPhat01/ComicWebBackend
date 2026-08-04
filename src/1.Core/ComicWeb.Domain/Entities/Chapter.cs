using ComicWeb.Domain.Common;
using ComicWeb.Domain.Enums;

namespace ComicWeb.Domain.Entities;

public class Chapter : BaseEntity
{
    public int StoryId { get; set; }
    public int ChapterNumber { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? AffiliateLink { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Story Story { get; set; } = null!;
    public string Slug { get; set; } = string.Empty;
    public ChapterStatus Status { get; private set; } = ChapterStatus.Draft;
    public DateTime? PublishedAt { get; private set; }
    public DateTime? ScheduledAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public int Version { get; private set; }

    public void UpdateContent(int number, string title, string slug, string content, DateTime now) { if (number <= 0 || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(slug)) throw new InvalidOperationException("Invalid chapter."); ChapterNumber = number; Title = title.Trim(); Slug = slug.Trim(); Content = content; UpdateAt = now; Version++; }
    public void Publish(DateTime now) { EnsurePublishable(); Status = ChapterStatus.Published; PublishedAt ??= now; ScheduledAt = null; UpdateAt = now; Version++; }
    public void Schedule(DateTime? scheduledAt, DateTime now)
    {
        EnsureActive();
        ScheduledAt = scheduledAt;
        UpdateAt = now;
        Version++;
    }
    public void Unpublish(DateTime now) { EnsureActive(); if (Status != ChapterStatus.Published) throw new InvalidOperationException("Invalid status transition."); Status = ChapterStatus.Draft; ScheduledAt = null; UpdateAt = now; Version++; }
    public void Hide(DateTime now) { EnsureActive(); if (Status != ChapterStatus.Published) throw new InvalidOperationException("Invalid status transition."); Status = ChapterStatus.Hidden; UpdateAt = now; Version++; }
    public void SoftDelete(DateTime now) { DeletedAt = now; UpdateAt = now; Version++; }
    public void Restore(DateTime now) { DeletedAt = null; UpdateAt = now; Version++; }
    private void EnsureActive() { if (DeletedAt is not null) throw new InvalidOperationException("Chapter is deleted."); }
    private void EnsurePublishable() { EnsureActive(); if (ChapterNumber <= 0 || string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Slug) || string.IsNullOrWhiteSpace(Content)) throw new InvalidOperationException("Chapter is not publishable."); }
}
