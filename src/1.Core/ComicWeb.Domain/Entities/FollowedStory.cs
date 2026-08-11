using System;
using ComicWeb.Domain.Common;

namespace ComicWeb.Domain.Entities;

public class FollowedStory : BaseEntity
{
    public int UserId { get; set; }
    public int StoryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public Story Story { get; set; } = null!;
}
