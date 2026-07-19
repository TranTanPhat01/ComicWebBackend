using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Dtos
{
    public record ChapterDetailResultDto(
        int Id,
        int ChapterNumber,
        string Title,
        string? Content,        // Có thể null nếu chưa được mở khóa
        string? AffiliateLink,  // Trả về link Shopee nếu chương yêu cầu unlock
        bool IsLocked
        );
}
