using System;
using ComicWeb.Domain.Common;
using ComicWeb.Domain.Enums;

namespace ComicWeb.Domain.Entities;

public class Comment : BaseEntity
{
    public int StoryId { get; set; }
    public int? ChapterId { get; set; }
    public int UserId { get; set; }
    public int? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public CommentStatus Status { get; set; } = CommentStatus.Active;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Story Story { get; set; } = null!;
    public Comment? ParentComment { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
}
