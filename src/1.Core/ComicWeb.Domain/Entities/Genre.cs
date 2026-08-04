using ComicWeb.Domain.Common;

namespace ComicWeb.Domain.Entities;

public class Genre : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Story> Stories { get; set; } = new List<Story>();
}
