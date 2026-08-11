using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Dtos;
using ComicWeb.Application.Features.Stories;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/admin/scraper")]
    [Authorize(Policy = "AdminOnly")]
    public sealed class AdminScraperController : BaseApiController
    {
        private readonly IScraperService _scraperService;

        public AdminScraperController(IScraperService scraperService)
        {
            _scraperService = scraperService;
        }

        [HttpPost("metadata")]
        public async Task<ActionResult<ApiEnvelope<ScrapedStoryMetadataDto>>> GetMetadata(
            [FromBody] ScrapeMetadataRequest body,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(body.Url))
            {
                return BadRequest(new ApiEnvelope<string>("URL is required.", RequestId()));
            }

            try
            {
                var result = await _scraperService.ScrapeStoryMetadataAsync(body.Url, cancellationToken);
                return Ok(new ApiEnvelope<ScrapedStoryMetadataDto>(result, RequestId()));
            }
            catch (Exception ex)
            {
                throw new ComicWeb.Application.Common.Exceptions.AppException(
                    "SCRAPE_ERROR",
                    Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest,
                    "Lỗi cào truyện",
                    $"Lỗi cào thông tin truyện: {ex.Message}");
            }
        }

        [HttpPost("stories/{storyId:int}/chapter")]
        public async Task<ActionResult<ApiEnvelope<int>>> ImportChapter(
            int storyId,
            [FromBody] ScrapeChapterRequest body,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(body.Url) || string.IsNullOrWhiteSpace(body.Title))
            {
                return BadRequest(new ApiEnvelope<string>("Url and Title are required.", RequestId()));
            }

            try
            {
                // Step 1: Scrape clean chapter text
                var cleanHtml = await _scraperService.ScrapeChapterContentAsync(body.Url, cancellationToken);

                // Step 2: Create chapter via mediator (using canonical CreateAdminChapterCommand)
                var command = new CreateAdminChapterCommand(
                    StoryId: storyId,
                    ChapterNumber: body.ChapterNumber,
                    Title: body.Title,
                    Slug: null,
                    Content: cleanHtml,
                    IsLocked: false,
                    AffiliateLink: null
                );

                var chapter = await Mediator.Send(command, cancellationToken);
                return Ok(new ApiEnvelope<int>(chapter.Id, RequestId()));
            }
            catch (Exception ex)
            {
                throw new ComicWeb.Application.Common.Exceptions.AppException(
                    "IMPORT_CHAPTER_ERROR",
                    Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest,
                    "Lỗi cào chương",
                    $"Lỗi cào và lưu chương: {ex.Message}");
            }
        }

        private string RequestId() => HttpContext.TraceIdentifier;
    }

    public record ScrapeMetadataRequest(string Url);

    public record ScrapeChapterRequest(string Url, int ChapterNumber, string Title);
}
