using ComicWeb.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Domain.Entities
{
    public class Chapter : BaseEntity
    {
        public int StoryId { get; set; } // Khóa ngoại liên kết tới bảng Story
        public int ChapterNumber { get; set; }
        public string? Title { get; set; } 
        public string? Content { get; set; }
        public string? AffiliateLink { get; set; }

        // SỬA TẠI ĐÂY: Đảm bảo thuộc tính có { get; set; } công khai, không bị read-only
        public bool IsLocked { get; set; }

        // BỔ SUNG TẠI ĐÂY: Thêm trường ngày tạo để hết lỗi gạch đỏ
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Story Story { get; set; } = null!;
    }
}
