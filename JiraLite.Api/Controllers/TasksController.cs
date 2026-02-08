using JiraLite.Application.DTOs;
using JiraLite.Application.Interfaces;
using JiraLite.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace JiraLite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // JWT required
    public class TasksController : ControllerBase
    {
        private readonly ITaskService _taskService;

        public TasksController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        // 🔹 Create a task (only project members)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TaskItemDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var task = await _taskService.CreateTaskAsync(dto, userId);
                return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
        }

        // 🔹 Get task by ID (any project member can view)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var task = await _taskService.GetTaskByIdAsync(id, userId);

                if (task == null)
                    return NotFound();

                return Ok(task);
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
        }

        // 🔹 Get tasks by project (only project members)
        [HttpGet("project/{projectId}")]
        public async Task<IActionResult> GetByProject(Guid projectId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var tasks = await _taskService.GetTasksByProjectAsync(projectId, userId);
                return Ok(tasks);
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
        }

        // 🔹 Update task (only assignee or project owner)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TaskItemDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var task = await _taskService.UpdateTaskAsync(id, dto, userId);
                return Ok(task);
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
        }

        // 🔹 Delete task (only assignee or project owner)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _taskService.DeleteTaskAsync(id, userId);
                return NoContent();
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
        }

        // 🔹 Helper to extract current user ID from JWT
        private Guid GetCurrentUserId()
        {
            return Guid.Parse(User.FindFirstValue("id")!);
        }
    }
}
