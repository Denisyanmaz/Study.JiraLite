using JiraLite.Application.DTOs.Common;
using JiraLite.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace JiraLite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ActivityController : ControllerBase
    {
        private readonly IActivityService _activity;

        public ActivityController(IActivityService activity)
        {
            _activity = activity;
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
