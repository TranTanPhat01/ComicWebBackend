using ComicWeb.Domain.Common;
using ComicWeb.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Domain.Entities
{
    public class Story : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CoverImageUrl { get; set; } = string.Empty;

        // Thay đổi từ string sang Enum
        public StoryStatus Status { get; set; } = StoryStatus.Ongoing;

        public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();

    }
}
