using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Dtos
{
    public record StoryDto(int Id,
        string Title,
        string Description,
        string CoverImageUrl,
        string Status // Sẽ được AutoMapper tự chuyển từ Enum sang String sạch sẽ
        );

}
