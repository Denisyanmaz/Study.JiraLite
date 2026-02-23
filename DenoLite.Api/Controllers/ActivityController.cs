using DenoLite.Application.DTOs.Common;
using DenoLite.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DenoLite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ActivityController : ControllerBase
    {
        private readonly IActivityService _activity;
        private readonly IWebHostEnvironment _env;

        public ActivityController(IActivityService activity, IWebHostEnvironment env)
        {
            _activity = activity;
            _env = env;
        }

        /// <summary>One-time fix: update existing CommentAdded activity messages to full comment body. Development only.</summary>
        [HttpPost("fix-comment-added-messages")]
        public async Task<IActionResult> FixCommentAddedMessages()
        {
            if (!_env.IsDevelopment())
                return NotFound();
            var updated = await _activity.FixCommentAddedMessagesAsync();
            return Ok(new { updated });
        }

        [HttpGet("project/{projectId}")]
        public async Task<IActionResult> GetByProject(Guid projectId, [FromQuery] ActivityFilterQueryDto query)
        {
            var result = await _activity.GetByProjectAsync(projectId, query, GetCurrentUserId());
            return Ok(result);
        }

        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetByTask(Guid taskId, [FromQuery] ActivityFilterQueryDto query)
        {
            var result = await _activity.GetByTaskAsync(taskId, query, GetCurrentUserId());
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
