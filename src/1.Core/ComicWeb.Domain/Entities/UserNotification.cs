using ComicWeb.Domain.Common;
using System;

namespace ComicWeb.Domain.Entities;

public class UserNotification : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public int? StoryId { get; set; }
    public Story? Story { get; set; }
    
    public int? ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
}
