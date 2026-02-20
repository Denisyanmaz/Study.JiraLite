using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.Project;
using DenoLite.Application.DTOs.ProjectMember;
using DenoLite.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DenoLite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;

        public ProjectsController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        // -----------------------------
        // Projects
        // -----------------------------

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
        {
            var result = await _projectService.CreateAsync(GetCurrentUserId(), dto);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> MyProjects()
        {
            var projects = await _projectService.GetMyProjectsAsync(GetCurrentUserId());
            return Ok(projects);
        }

        [HttpGet("paged")]
        public async Task<IActionResult> MyProjectsPaged([FromQuery] TasksPagedQueryDto q)
        {
            var result = await _projectService.GetMyProjectsPagedAsync(GetCurrentUserId(), q.Page, q.PageSize);
            return Ok(result);
        }

        // -----------------------------
        // Members
        // -----------------------------

        // POST /api/projects/{projectId}/members
        [HttpPost("{projectId:guid}/members")]
        public async Task<IActionResult> AddMember(Guid projectId, [FromBody] AddProjectMemberDto dto)
        {
            var currentUserId = GetCurrentUserId();

            var memberDto = await _projectService.AddMemberAsync(projectId, dto, currentUserId);

            // returns the member that was added
            return CreatedAtAction(nameof(GetMembers), new { projectId = projectId }, memberDto);
        }

        // GET /api/projects/{projectId}/members
        [HttpGet("{projectId:guid}/members")]
        public async Task<IActionResult> GetMembers(Guid projectId)
        {
            var currentUserId = GetCurrentUserId();

            var members = await _projectService.GetMembersAsync(projectId, currentUserId);

            return Ok(members);
        }

        // DELETE /api/projects/{projectId}/members/{memberUserId}
        [HttpDelete("{projectId:guid}/members/{memberUserId:guid}")]
        public async Task<IActionResult> RemoveMember(Guid projectId, Guid memberUserId)
        {
            var currentUserId = GetCurrentUserId();

            await _projectService.RemoveMemberAsync(projectId, memberUserId, currentUserId);

            return NoContent();
        }

        // -----------------------------
        // Helpers
        // -----------------------------

        private Guid GetCurrentUserId()
        {
            // ✅ Ensure user is authenticated (should be guaranteed by [Authorize], but double-check)
            if (User?.Identity?.IsAuthenticated != true)
                throw new UnauthorizedAccessException("User is not authenticated");

            var idClaim =
                User.FindFirstValue("id") ??
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(idClaim))
                throw new UnauthorizedAccessException("User ID claim missing");

            if (!Guid.TryParse(idClaim, out var userId))
                throw new UnauthorizedAccessException($"Invalid user ID claim: '{idClaim}'");

            return userId;
        }
    }
}
