using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Common;
using JiraLite.Application.DTOs.Tasks;
using JiraLite.Application.Exceptions;
using JiraLite.Application.Interfaces;
using JiraLite.Domain.Entities;
using JiraLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JiraLite.Infrastructure.Services
{
    public class TaskService : ITaskService
    {
        private readonly JiraLiteDbContext _context;

        public TaskService(JiraLiteDbContext context)
        {
            _context = context;
        }

        // 🔹 Create Task (only project members)
        public async Task<TaskItem> CreateTaskAsync(TaskItemDto dto, Guid currentUserId)
        {
            var isMember = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == dto.ProjectId && pm.UserId == currentUserId);

            if (!isMember)
                throw new ForbiddenException("Only project members can create tasks.");

            var task = new TaskItem
            {
                Title = dto.Title,
                Description = dto.Description,
                Status = dto.Status,
                Priority = dto.Priority,
                AssigneeId = dto.AssigneeId,
                ProjectId = dto.ProjectId,
                DueDate = dto.DueDate
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();
            return task;
        }

        // 🔹 Get Task by ID (only project members)
        public async Task<TaskItem?> GetTaskByIdAsync(Guid taskId, Guid currentUserId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null) return null;

            var isMember = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == task.ProjectId && pm.UserId == currentUserId);

            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            return task;
        }

        // 🔹 Get Tasks by Project (only project members)
        public async Task<List<TaskItem>> GetTasksByProjectAsync(Guid projectId, Guid currentUserId)
        {
            var isMember = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId);

            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            return await _context.Tasks
                .Where(t => t.ProjectId == projectId)
                .ToListAsync();
        }

        // 🔹 Update Task (only assignee or project owner)
        public async Task<TaskItem> UpdateTaskAsync(Guid taskId, TaskItemDto dto, Guid currentUserId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null) throw new NotFoundException("Task not found");

            var project = await _context.Projects.FindAsync(task.ProjectId);
            if (project == null) throw new NotFoundException("Project not found");

            if (task.AssigneeId != currentUserId && project.OwnerId != currentUserId)
                throw new ForbiddenException("Only the assignee or project owner can update this task.");

            task.Title = dto.Title;
            task.Description = dto.Description;
            task.Status = dto.Status;
            task.Priority = dto.Priority;
            task.AssigneeId = dto.AssigneeId;
            task.DueDate = dto.DueDate;

            await _context.SaveChangesAsync();
            return task;
        }

        // 🔹 Delete Task (only assignee or project owner)
        public async Task DeleteTaskAsync(Guid taskId, Guid currentUserId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null) throw new NotFoundException("Task not found");

            var project = await _context.Projects.FindAsync(task.ProjectId);
            if (project == null) throw new NotFoundException("Project not found");

            if (task.AssigneeId != currentUserId && project.OwnerId != currentUserId)
                throw new ForbiddenException("Only the assignee or project owner can delete this task.");

            // ✅ Soft delete instead of physical delete
            task.IsDeleted = true;
            task.DeletedAt = DateTime.UtcNow;
            task.DeletedBy = currentUserId;

            await _context.SaveChangesAsync();
        }


        public async Task<PagedResult<TaskItem>> GetTasksByProjectPagedAsync(
            Guid projectId,
            Guid currentUserId,
            TaskQueryDto query)
        {
            var isMember = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId);

            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            // Base query scoped to project
            IQueryable<TaskItem> tasks = _context.Tasks.Where(t => t.ProjectId == projectId);

            // Filters
            if (query.Status.HasValue)
                tasks = tasks.Where(t => t.Status == query.Status.Value);

            if (query.Priority.HasValue)
                tasks = tasks.Where(t => t.Priority == query.Priority.Value);

            if (query.AssigneeId.HasValue)
                tasks = tasks.Where(t => t.AssigneeId == query.AssigneeId.Value);

            if (query.DueFrom.HasValue)
                tasks = tasks.Where(t => t.DueDate.HasValue && t.DueDate.Value >= query.DueFrom.Value);

            if (query.DueTo.HasValue)
                tasks = tasks.Where(t => t.DueDate.HasValue && t.DueDate.Value <= query.DueTo.Value);

            var total = await tasks.CountAsync();

            // Deterministic ordering for stable paging + tests
            var items = await tasks
                .OrderBy(t => t.DueDate == null)     // tasks with DueDate first (false before true)
                .ThenBy(t => t.DueDate)              // then by due date
                .ThenBy(t => t.Id)                   // tie-breaker
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return new PagedResult<TaskItem>(items, query.Page, query.PageSize, total);
        }
    }
}
