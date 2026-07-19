using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Dtos
{
    public record ChapterListDto(
        int Id,
        int ChapterNumber,
        string Title,
        DateTime CreatedAt,
        bool IsLocked
        );
}
