using DenoLite.Application.DTOs.Comment;
using DenoLite.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DenoLite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentService _comments;

        public CommentsController(ICommentService comments)
        {
            _comments = comments;
        }

        [HttpPost("task/{taskId}")]
        public async Task<IActionResult> AddToTask(Guid taskId, [FromBody] CreateCommentDto dto)
        {
            var result = await _comments.AddToTaskAsync(taskId, dto, GetCurrentUserId());
            return CreatedAtAction(nameof(GetByTask), new { taskId = taskId }, result);
        }

        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetByTask(Guid taskId)
        {
            var result = await _comments.GetByTaskAsync(taskId, GetCurrentUserId());
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var idClaim =
                User.FindFirstValue("id") ??
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(idClaim, out var userId))
                throw new UnauthorizedAccessException("Invalid user id");

            return userId;
        }
    }
}
