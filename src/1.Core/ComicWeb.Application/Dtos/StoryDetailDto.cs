using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Dtos
{
    public record StoryDetailDto(
        int Id,
        string Title,
        string Description,
        string CoverImageUrl,
        string? AuthorName,
        string Status,
        List<ChapterListDto> Chapters
        );

}
