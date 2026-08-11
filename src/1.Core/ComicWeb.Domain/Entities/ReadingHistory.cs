using System;
using ComicWeb.Domain.Common;

namespace ComicWeb.Domain.Entities;

public class ReadingHistory : BaseEntity
{
    public int UserId { get; set; }
    public int StoryId { get; set; }
    public int ChapterId { get; set; }
    public DateTime LastReadAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public Story Story { get; set; } = null!;
    public Chapter Chapter { get; set; } = null!;
}
