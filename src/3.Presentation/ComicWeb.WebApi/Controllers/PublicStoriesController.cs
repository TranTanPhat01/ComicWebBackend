using ComicWeb.Application.Features.Stories;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Controllers;

[ApiController]
[Route("api/v1/stories")]
[AllowAnonymous]
public sealed class PublicStoriesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page=1,[FromQuery] int pageSize=20,[FromQuery] string? query=null,[FromQuery] string? author=null,[FromQuery] string? genre=null,[FromQuery] string sort="-updatedAt")
    {
        var result=await Mediator.Send(new GetPublishedStoriesQuery(page,pageSize,query,author,genre,sort));
        return Ok(new PagedApiEnvelope<PublicStoryListItemDto>(result.Items,result.Meta,HttpContext.TraceIdentifier));
    }

    [HttpGet("genres")]
    public async Task<IActionResult> Genres()
    {
        var result = await Mediator.Send(new GetGenresQuery());
        return Ok(new ApiEnvelope<IReadOnlyList<GenreListItemDto>>(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{storySlug}")]
    public async Task<IActionResult> Detail(string storySlug)
    {
        var result = await Mediator.Send(new GetPublishedStoryBySlugQuery(storySlug));
        if (HandleConditionalGet(result, HttpContext.TraceIdentifier, out var actionResult))
        {
            return actionResult!;
        }
        return Ok(new ApiEnvelope<PublicStoryDetailDto>(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{storySlug}/chapters")]
    public async Task<IActionResult> Chapters(string storySlug,[FromQuery]int page=1,[FromQuery]int pageSize=100,[FromQuery]string sort="chapterNumber")
    {
        var result=await Mediator.Send(new GetPublishedChaptersByStorySlugQuery(storySlug,page,pageSize,sort));
        return Ok(new PagedApiEnvelope<PublicChapterSummaryDto>(result.Items,result.Meta,HttpContext.TraceIdentifier));
    }

    [HttpGet("{storySlug}/chapters/{chapterSlug}")]
    public async Task<IActionResult> Chapter(string storySlug,string chapterSlug)
    {
        var result = await Mediator.Send(new GetPublishedChapterBySlugQuery(storySlug,chapterSlug));
        if (HandleConditionalGet(result, HttpContext.TraceIdentifier, out var actionResult))
        {
            return actionResult!;
        }
        return Ok(new ApiEnvelope<PublicChapterDetailDto>(result, HttpContext.TraceIdentifier));
    }

    private bool HandleConditionalGet(object dto, string traceId, out IActionResult? actionResult)
    {
        actionResult = null;
        var json = System.Text.Json.JsonSerializer.Serialize(dto);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
        var etag = $"\"{Convert.ToBase64String(hash)}\"";

        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "public, no-cache";
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
