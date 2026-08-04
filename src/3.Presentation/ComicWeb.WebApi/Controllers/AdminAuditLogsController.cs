using ComicWeb.Application.Common.Interfaces;
using ComicWeb.WebApi.Controllers;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Controllers;

[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminAuditLogsController : BaseApiController
{
    private readonly IAuditLogQueryService _queryService;

    public AdminAuditLogsController(IAuditLogQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedApiEnvelope<AuditLogDto>>> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? actorUserId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? result = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
    {
        var query = new AuditLogQuery(
            page,
            pageSize,
            actorUserId,
            action,
            entityType,
            entityId,
            result,
            fromUtc,
            toUtc);

        var resultData = await _queryService.SearchAsync(query, HttpContext.RequestAborted);
        return Ok(new PagedApiEnvelope<AuditLogDto>(resultData.Items, resultData.Meta, HttpContext.TraceIdentifier));
    }
}
