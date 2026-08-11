using ComicWeb.Application.Features.Admin.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComicWeb.WebApi.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : BaseApiController
    {
        // GET api/admin/stats — Dashboard metrics
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
            => Ok(await Mediator.Send(new GetAdminStatsQuery()));

        // GET api/admin/logs?page=1&pageSize=20 — System logs
        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(await Mediator.Send(new GetSystemLogsQuery(page, pageSize)));
    }
}
