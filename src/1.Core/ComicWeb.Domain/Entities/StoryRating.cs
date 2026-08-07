using System;
using ComicWeb.Domain.Common;

namespace ComicWeb.Domain.Entities;

public class StoryRating : BaseEntity
{
    public int UserId { get; set; }
    public int StoryId { get; set; }
    public int Score { get; set; } // 1-5

    // Navigation properties
    public User User { get; set; } = null!;
    public Story Story { get; set; } = null!;
}
