using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ComicWeb.Domain.Enums;
using System.Text.Json.Serialization;

namespace ComicWeb.Application.Dtos
{
    public record StoryDetailDto(
        int Id,
        string Title,
        string Description,
        string CoverImageUrl,
        string? AuthorName,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        StoryStatus Status,
        List<ChapterListDto> Chapters
        );

}
