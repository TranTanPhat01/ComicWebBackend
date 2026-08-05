using ComicWeb.Application.Features.Stories;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Controllers;

[ApiController]
[Route("api/v1/stories")]
[AllowAnonymous]
[EnableRateLimiting("public-reading")]
public sealed class PublicStoriesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page=1,[FromQuery] int pageSize=20,[FromQuery] string? query=null,[FromQuery] string? author=null,[FromQuery] string? genre=null,[FromQuery] string sort="-updatedAt")
    {
        var result=await Mediator.Send(new GetPublishedStoriesQuery(page,pageSize,query,author,genre,sort));
        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "public, max-age=10, s-maxage=60, stale-while-revalidate=30";
        return Ok(new PagedApiEnvelope<PublicStoryListItemDto>(result.Items,result.Meta,HttpContext.TraceIdentifier));
    }

    [HttpGet("genres")]
    public async Task<IActionResult> Genres()
    {
        var result = await Mediator.Send(new GetGenresQuery());
        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "public, max-age=60, s-maxage=300, stale-while-revalidate=120";
        return Ok(new ApiEnvelope<IReadOnlyList<GenreListItemDto>>(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{storySlug}")]
    public async Task<IActionResult> Detail(string storySlug)
    {
        var result = await Mediator.Send(new GetPublishedStoryBySlugQuery(storySlug));
        if (HandleConditionalGet(result.Version, result.UpdatedAt, "public, max-age=60, s-maxage=3600, stale-while-revalidate=120", HttpContext.TraceIdentifier, out var actionResult))
        {
            return actionResult!;
        }
        return Ok(new ApiEnvelope<PublicStoryDetailDto>(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{storySlug}/chapters")]
    public async Task<IActionResult> Chapters(string storySlug,[FromQuery]int? lastChapterNumber=null,[FromQuery]int pageSize=100)
    {
        var result=await Mediator.Send(new GetPublishedChaptersByStorySlugQuery(storySlug,lastChapterNumber,pageSize));
        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "public, max-age=60, s-maxage=600, stale-while-revalidate=60";
        return Ok(new PagedApiEnvelope<PublicChapterSummaryDto>(
            result.Items,
            new { nextCursor = result.NextCursor, hasMore = result.HasMore },
            HttpContext.TraceIdentifier));
    }

    [HttpGet("{storySlug}/chapters/{chapterSlug}")]
    public async Task<IActionResult> Chapter(string storySlug,string chapterSlug)
    {
        var result = await Mediator.Send(new GetPublishedChapterBySlugQuery(storySlug,chapterSlug));
        if (HandleConditionalGet(result.Version, result.UpdatedAt, "public, max-age=60, s-maxage=3600, stale-while-revalidate=120", HttpContext.TraceIdentifier, out var actionResult))
        {
            return actionResult!;
        }
        return Ok(new ApiEnvelope<PublicChapterDetailDto>(result, HttpContext.TraceIdentifier));
    }

    private bool HandleConditionalGet(int version, DateTime? updatedAt, string cacheControl, string traceId, out IActionResult? actionResult)
    {
        actionResult = null;
        var etagValue = $"{version}_{updatedAt?.Ticks ?? 0}";
        var etag = $"\"{etagValue}\"";

        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = cacheControl;
        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.ETag] = etag;

        var requestHeaders = Request.Headers;
        if (requestHeaders.TryGetValue(Microsoft.Net.Http.Headers.HeaderNames.IfNoneMatch, out var ifNoneMatch) && ifNoneMatch == etag)
        {
            actionResult = StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status304NotModified);
            return true;
        }

        return false;
    }
}
