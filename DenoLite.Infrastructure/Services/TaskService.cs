using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.Task;
using DenoLite.Application.Exceptions;
using DenoLite.Application.Interfaces;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DenoLite.Infrastructure.Services
{
    public class TaskService : ITaskService
    {
        private readonly DenoLiteDbContext _context;
        private readonly IActivityService _activity;
        private readonly IEmailSender _emailSender;

        public TaskService(DenoLiteDbContext context, IActivityService activity, IEmailSender emailSender)
        {
            _context = context;
            _activity = activity;
            _emailSender = emailSender;
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
                DueDate = AsUtc(dto.DueDate)
            };


            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            var assigneeEmail = await GetUserEmailAsync(task.AssigneeId);
            await _activity.LogAsync(
                projectId: task.ProjectId,
                taskId: task.Id,
                actorId: currentUserId,
                actionType: "TaskCreated",
                message: $"Task created: '{task.Title}' (Status: {task.Status}, Priority: {FormatPriority(task.Priority)}, Assignee: {assigneeEmail}, Due: {FormatDue(task.DueDate)})"
            );

            await SendAssignmentEmailAsync(task.AssigneeId, task.Title, task.Id);

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
            task.DueDate = AsUtc(dto.DueDate);

            await _context.SaveChangesAsync();

            // ✅ build rich diff message
            static string P(int p) => $"P{p}";
            static string Due(DateTime? d) => d.HasValue ? d.Value.ToString("dd.MM.yyyy") : "None";

            var changes = new List<string>();

            if (beforeTitle != task.Title)
                changes.Add($"Title '{beforeTitle}' → '{task.Title}'");

            if (beforeStatus != task.Status)
                changes.Add($"Status {beforeStatus} → {task.Status}");

            if (beforePriority != task.Priority)
                changes.Add($"Priority {P(beforePriority)} → {P(task.Priority)}");

            if (beforeAssignee != task.AssigneeId)
                changes.Add($"Assignee {await GetUserEmailAsync(beforeAssignee)} → {await GetUserEmailAsync(task.AssigneeId)}");

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

            if (beforeAssignee != task.AssigneeId)
                await SendAssignmentEmailAsync(task.AssigneeId, task.Title, task.Id);

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

            var dueFrom = AsUtc(query.DueFrom);
            var dueTo   = AsUtc(query.DueTo);

            if (dueFrom.HasValue)
                tasks = tasks.Where(t => t.DueDate.HasValue && t.DueDate.Value >= dueFrom.Value);

            if (dueTo.HasValue)
                tasks = tasks.Where(t => t.DueDate.HasValue && t.DueDate.Value <= dueTo.Value);

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
        public async Task<TaskItem> UpdateTaskStatusAsync(Guid taskId, DenoTaskStatus status, Guid currentUserId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null) throw new NotFoundException("Task not found");

            var project = await _context.Projects.FindAsync(task.ProjectId);
            if (project == null) throw new NotFoundException("Project not found");

            if (task.AssigneeId != currentUserId && project.OwnerId != currentUserId)
                throw new ForbiddenException("Only the assignee or project owner can update this task.");

            var beforeStatus = task.Status;
            if (beforeStatus != status)
            {
                task.Status = status;
                await _context.SaveChangesAsync();

                // optional: activity log
                await _activity.LogAsync(task.ProjectId, task.Id, currentUserId, "TaskStatusChanged",
                    $"Status changed: {beforeStatus} → {task.Status}.");
            }

            return task;
        }


        private async Task<string> GetUserEmailAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user?.Email ?? userId.ToString();
        }

        private async Task SendAssignmentEmailAsync(Guid assigneeId, string taskTitle, Guid taskId)
        {
            try
            {
                var user = await _context.Users.FindAsync(assigneeId);
                if (user == null || string.IsNullOrWhiteSpace(user.Email)) return;

                var subject = $"You have been assigned a task: \"{taskTitle}\"";
                var body = $"""
                    <p>Hi,</p>
                    <p>You have been assigned the following task in <strong>DenoLite</strong>:</p>
                    <p><strong>{System.Net.WebUtility.HtmlEncode(taskTitle)}</strong></p>
                    <p><a href="/Tasks/Details/{taskId}?tab=overview">View task</a></p>
                    <p>— The DenoLite Team</p>
                    """;

                await _emailSender.SendAsync(user.Email, subject, body);
            }
            catch
            {
                // email failure must never break task operations
            }
        }

        private static string FormatPriority(int p) => $"P{p}";

        private static string FormatDue(DateTime? due)
            => due.HasValue ? due.Value.ToString("dd.MM.yyyy") : "None";

        private static string Short(string? text, int max = 80)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
        private static DateTime? AsUtc(DateTime? dt)
        {
            if (!dt.HasValue) return null;

            // if it already has a kind, keep it (but convert Local -> UTC)
            if (dt.Value.Kind == DateTimeKind.Utc) return dt.Value;
            if (dt.Value.Kind == DateTimeKind.Local) return dt.Value.ToUniversalTime();

            // Unspecified -> treat as UTC (best for date-only inputs)
            return DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc);
        }
    }
}
