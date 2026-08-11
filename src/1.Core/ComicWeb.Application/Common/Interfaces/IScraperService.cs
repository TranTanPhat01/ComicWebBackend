using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Dtos;

namespace ComicWeb.Application.Common.Interfaces
{
    public interface IScraperService
    {
        /// <summary>
        /// Crawls the main story page HTML to extract details and list of chapter URLs.
        /// </summary>
        Task<ScrapedStoryMetadataDto> ScrapeStoryMetadataAsync(string storyUrl, CancellationToken ct);

        /// <summary>
        /// Crawls a chapter page HTML to extract the clean main text content.
        /// </summary>
        Task<string> ScrapeChapterContentAsync(string chapterUrl, CancellationToken ct);
    }
}
