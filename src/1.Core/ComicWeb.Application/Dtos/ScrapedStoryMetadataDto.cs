using System.Collections.Generic;

namespace ComicWeb.Application.Dtos
{
    public record ScrapedStoryMetadataDto(
        string Title,
        string Description,
        string CoverImageUrl,
        string AuthorName,
        List<string> Genres,
        List<ScrapedChapterLinkDto> Chapters
    );

    public record ScrapedChapterLinkDto(
        int ChapterNumber,
        string Title,
        string Url
    );
}
