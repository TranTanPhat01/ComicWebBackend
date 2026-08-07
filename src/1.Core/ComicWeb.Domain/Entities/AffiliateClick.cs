using System;
using ComicWeb.Domain.Common;

namespace ComicWeb.Domain.Entities;

public class AffiliateClick : BaseEntity
{
    public int ChapterId { get; set; }
    public int StoryId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Referrer { get; set; }
    public DateTime ClickedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Chapter Chapter { get; set; } = null!;
    public Story Story { get; set; } = null!;
}
