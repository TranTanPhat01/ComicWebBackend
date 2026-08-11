using ComicWeb.Application.Features.Stories.Commands;
using ComicWeb.Application.Features.Stories.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComicWeb.WebApi.Controllers
{
    public class ChaptersController : BaseApiController
    {
        // ── PUBLIC ─────────────────────────────────────────────────────

        // GET api/chapters/{id} — Đọc chương (có check IsLocked)
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var chapter = await Mediator.Send(new GetChapterDetailQuery(id));
            return chapter == null
                ? NotFound(new { message = "Không tìm thấy chương." })
                : Ok(chapter);
        }

        // ── ADMIN ──────────────────────────────────────────────────────

        // POST api/chapters — Tạo chương mới
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create([FromBody] CreateChapterCommand command)
        {
            var id = await Mediator.Send(command);
            return Ok(new { id, message = "Thêm chương mới thành công!" });
        }

        // PUT api/chapters/{id} — Cập nhật chương
        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateChapterCommand command)
        {
            if (id != command.Id)
                return BadRequest(new { message = "Id không khớp." });

            var ok = await Mediator.Send(command);
            return ok
                ? Ok(new { message = "Cập nhật chương thành công!" })
                : NotFound(new { message = $"Không tìm thấy chương Id = {id}" });
        }

        // DELETE api/chapters/{id} — Xóa chương
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await Mediator.Send(new DeleteChapterCommand(id));
            return ok
                ? Ok(new { message = "Xóa chương thành công!" })
                : NotFound(new { message = $"Không tìm thấy chương Id = {id}" });
        }
    }
}
