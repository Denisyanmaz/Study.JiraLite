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
        private readonly IActivityService _activity;

        public TaskService(JiraLiteDbContext context, IActivityService activity)
        {
            _context = context;
            _activity = activity;
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

            await _activity.LogAsync(
                projectId: task.ProjectId,
                taskId: task.Id,
                actorId: currentUserId,
                actionType: "TaskCreated",
                message: $"Task created: '{task.Title}' (Status: {task.Status}, Priority: {FormatPriority(task.Priority)}, Assignee: {task.AssigneeId}, Due: {FormatDue(task.DueDate)})"
            );

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

            // ✅ snapshot (before)
            var beforeTitle = task.Title;
            var beforeStatus = task.Status;
            var beforePriority = task.Priority;
            var beforeAssignee = task.AssigneeId;
            var beforeDue = task.DueDate;

            // apply updates
            task.Title = dto.Title;
            task.Description = dto.Description;
            task.Status = dto.Status;
            task.Priority = dto.Priority;
            task.AssigneeId = dto.AssigneeId;
            task.DueDate = dto.DueDate;

            await _context.SaveChangesAsync();

            // ✅ build rich diff message
            static string P(int p) => $"P{p}";
            static string Due(DateTime? d) => d.HasValue ? d.Value.ToString("yyyy-MM-dd") : "None";

            var changes = new List<string>();

            if (beforeTitle != task.Title)
                changes.Add($"Title '{beforeTitle}' → '{task.Title}'");

            if (beforeStatus != task.Status)
                changes.Add($"Status {beforeStatus} → {task.Status}");

            if (beforePriority != task.Priority)
                changes.Add($"Priority {P(beforePriority)} → {P(task.Priority)}");

            if (beforeAssignee != task.AssigneeId)
                changes.Add($"Assignee {beforeAssignee} → {task.AssigneeId}");

            if (beforeDue != task.DueDate)
                changes.Add($"Due {Due(beforeDue)} → {Due(task.DueDate)}");

            var message = changes.Count == 0
                ? $"Task updated: '{task.Title}'"
                : $"Task updated: '{task.Title}': {string.Join("; ", changes)}";

            await _activity.LogAsync(
                projectId: task.ProjectId,
                taskId: task.Id,
                actorId: currentUserId,
                actionType: "TaskUpdated",
                message: message
            );

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

            await _activity.LogAsync(
                projectId: task.ProjectId,
                taskId: task.Id,
                actorId: currentUserId,
                actionType: "TaskDeleted",
                message: $"Task deleted: '{task.Title}' (moved to recycle bin)"
            );
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

        private static string FormatPriority(int p) => $"P{p}";

        private static string FormatDue(DateTime? due)
            => due.HasValue ? due.Value.ToString("yyyy-MM-dd") : "None";

        private static string Short(string? text, int max = 80)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }
}
