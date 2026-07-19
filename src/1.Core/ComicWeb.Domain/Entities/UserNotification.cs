using ComicWeb.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Domain.Entities
{
    public class UserNotification : BaseEntity
    {
        public int? StoryId { get; set; }
        public int? ChapterId { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
    }
}
