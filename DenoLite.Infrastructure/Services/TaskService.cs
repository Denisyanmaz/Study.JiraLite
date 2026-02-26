using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.Task;
using DenoLite.Application.Exceptions;
using DenoLite.Application.Interfaces;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

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

            // Assignee must be an ACTIVE member of the project
            var assigneeIsMember = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == dto.ProjectId && pm.UserId == dto.AssigneeId);

            if (!assigneeIsMember)
                throw new BadRequestException("Assignee must be a current project member.");

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

            // Optional description preview for activity log (plain text)
            var descriptionPreview = Short(Plain(task.Description), 120);
            var createdMessage = new StringBuilder();
            createdMessage.Append(
                $"Task created: '{task.Title}' (Status: {task.Status}, Priority: {FormatPriority(task.Priority)}, Assignee: {assigneeEmail}, Due: {FormatDue(task.DueDate)})");
            if (!string.IsNullOrWhiteSpace(descriptionPreview))
            {
                createdMessage.Append($" Description: \"{descriptionPreview}\"");
            }

            await _activity.LogAsync(
                projectId: task.ProjectId,
                taskId: task.Id,
                actorId: currentUserId,
                actionType: "TaskCreated",
                message: createdMessage.ToString()
            );

            // Don't await: email must not block the response (SMTP can hang on Render)
            _ = SendAssignmentEmailAsync(task.AssigneeId, task.Title, task.Id);

            // Mentions: run in-request so we can safely use DbContext and log activity
            await NotifyMentionedUsersAsync(task, currentUserId);

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

            // Assignee must be an ACTIVE member of the project
            var assigneeIsMember = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == task.ProjectId && pm.UserId == dto.AssigneeId);

            if (!assigneeIsMember)
                throw new BadRequestException("Assignee must be a current project member.");

            // ✅ snapshot (before)
            var beforeTitle = task.Title;
            var beforeStatus = task.Status;
            var beforePriority = task.Priority;
            var beforeDescription = task.Description;
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

            if (!string.Equals(Plain(beforeDescription), Plain(task.Description), StringComparison.Ordinal))
            {
                var beforeShort = Short(Plain(beforeDescription), 80);
                var afterShort  = Short(Plain(task.Description), 80);
                changes.Add($"Description \"{beforeShort}\" → \"{afterShort}\"");
            }

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
                _ = SendAssignmentEmailAsync(task.AssigneeId, task.Title, task.Id);

            // Notify current assignee about the update (diff-based message).
            // Skip if the actor is the assignee themselves to avoid noisy self-emails.
            if (task.AssigneeId != Guid.Empty && task.AssigneeId != currentUserId)
            {
                var assigneeEmailForUpdate = await GetUserEmailAsync(task.AssigneeId);
                _ = SendTaskUpdateEmailAsync(assigneeEmailForUpdate, task.Title, task.Id, message);
            }

            // Mentions: run in-request so we can safely use DbContext
            await NotifyMentionedUsersAsync(task, currentUserId);

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

        public async Task<PagedResult<TaskItemBoardDto>> GetTasksByProjectPagedAsync(
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

            // Join with Users to get assignee email (including assignees who left the project)
            var items = await tasks
                .Join(_context.Users, t => t.AssigneeId, u => u.Id, (t, u) => new { t, u })
                .OrderBy(x => x.t.DueDate == null)
                .ThenBy(x => x.t.DueDate)
                .ThenBy(x => x.t.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(x => new TaskItemBoardDto
                {
                    Id = x.t.Id,
                    Title = x.t.Title,
                    Description = x.t.Description,
                    Status = x.t.Status,
                    Priority = x.t.Priority,
                    AssigneeId = x.t.AssigneeId,
                    AssigneeEmail = x.u.Email,
                    ProjectId = x.t.ProjectId,
                    DueDate = x.t.DueDate
                })
                .ToListAsync();

            return new PagedResult<TaskItemBoardDto>(items, query.Page, query.PageSize, total);
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

                var diffMessage = $"Status changed: {beforeStatus} → {task.Status}.";

                // activity log
                await _activity.LogAsync(task.ProjectId, task.Id, currentUserId, "TaskStatusChanged", diffMessage);

                // Notify assignee (if someone else changed the status)
                if (task.AssigneeId != Guid.Empty && task.AssigneeId != currentUserId)
                {
                    var assigneeEmailForStatus = await GetUserEmailAsync(task.AssigneeId);
                    _ = SendTaskUpdateEmailAsync(assigneeEmailForStatus, task.Title, task.Id, diffMessage);
                }
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

        private async Task SendTaskUpdateEmailAsync(string email, string taskTitle, Guid taskId, string diffMessage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email)) return;

                var subject = $"Task updated: \"{taskTitle}\"";
                var safeDiff = System.Net.WebUtility.HtmlEncode(diffMessage ?? string.Empty);

                var body = $"""
                    <p>Hi,</p>
                    <p>A task assigned to you was updated in <strong>DenoLite</strong>:</p>
                    <p><strong>{System.Net.WebUtility.HtmlEncode(taskTitle)}</strong></p>
                    <p style="white-space:pre-wrap;">{safeDiff}</p>
                    <p><a href="/Tasks/Details/{taskId}?tab=overview">View task</a></p>
                    <p>— The DenoLite Team</p>
                    """;

                await _emailSender.SendAsync(email, subject, body);
            }
            catch
            {
                // update email failure must never break task operations
            }
        }

        /// <summary>
        /// Finds @mentions in the task description, resolves them to project members by email local-part,
        /// and sends notification emails. Best-effort, non-blocking.
        /// </summary>
        private async Task NotifyMentionedUsersAsync(TaskItem task, Guid actorId)
        {
            // Nothing to do if there's no description
            if (string.IsNullOrWhiteSpace(task.Description))
                return;

            var handles = ExtractMentionHandles(task.Description);
            if (handles.Count == 0)
                return;

            try
            {
                // Load project name (optional, for nicer subject/body)
                var project = await _context.Projects.FindAsync(task.ProjectId);
                var projectName = project?.Name ?? string.Empty;

                // Load active project members with emails
                var members = await (
                    from pm in _context.ProjectMembers
                    join u in _context.Users on pm.UserId equals u.Id
                    where pm.ProjectId == task.ProjectId && !string.IsNullOrEmpty(u.Email)
                    select new { pm.UserId, u.Email }
                ).ToListAsync();

                if (members.Count == 0)
                    return;

                var handleSet = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);
                var targets = new Dictionary<Guid, string>();

                foreach (var m in members)
                {
                    if (string.IsNullOrWhiteSpace(m.Email)) continue;

                    var atIndex = m.Email.IndexOf('@');
                    var localPart = atIndex > 0 ? m.Email[..atIndex] : m.Email;

                    if (handleSet.Contains(localPart) && m.UserId != actorId)
                    {
                        // Distinct per user
                        targets[m.UserId] = m.Email;
                    }
                }

                if (targets.Count == 0)
                    return;

                foreach (var (_, email) in targets)
                {
                    _ = SendMentionEmailAsync(email, task, projectName);
                }
            }
            catch
            {
                // Mention notifications are best-effort and must never break task flows
            }
        }

        private static List<string> ExtractMentionHandles(string? text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            // Look for patterns like "@denowar92" in the (HTML) description.
            var matches = Regex.Matches(text, @"@([a-zA-Z0-9._%+-]+)");
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var handle = match.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(handle) &&
                        !result.Contains(handle, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(handle);
                    }
                }
            }

            return result;
        }

        private async Task SendMentionEmailAsync(string email, TaskItem task, string? projectName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return;

                var safeTitle = System.Net.WebUtility.HtmlEncode(task.Title ?? string.Empty);
                var safeProject = string.IsNullOrWhiteSpace(projectName)
                    ? string.Empty
                    : System.Net.WebUtility.HtmlEncode(projectName);

                var subject = string.IsNullOrWhiteSpace(safeProject)
                    ? $"You were mentioned in task \"{safeTitle}\""
                    : $"You were mentioned in \"{safeTitle}\" ({safeProject})";

                var bodyBuilder = new StringBuilder();
                bodyBuilder.Append("<p>Hi,</p>");
                bodyBuilder.Append("<p>You were mentioned in a task in <strong>DenoLite</strong>");
                if (!string.IsNullOrWhiteSpace(safeProject))
                {
                    bodyBuilder.Append($" (project <strong>{safeProject}</strong>)");
                }
                bodyBuilder.Append(":</p>");

                bodyBuilder.Append($"<p><strong>{safeTitle}</strong></p>");

                if (!string.IsNullOrWhiteSpace(task.Description))
                {
                    // Task description is already HTML from the editor; include as-is.
                    bodyBuilder.Append("<hr/>");
                    bodyBuilder.Append("<div>");
                    bodyBuilder.Append(task.Description);
                    bodyBuilder.Append("</div>");
                }

                bodyBuilder.Append(
                    $@"<p><a href=""/Tasks/Details/{task.Id}?tab=overview"">View task</a></p>");
                bodyBuilder.Append("<p>— The DenoLite Team</p>");

                await _emailSender.SendAsync(email, subject, bodyBuilder.ToString());
            }
            catch
            {
                // Mention email failure must never break task operations
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
        private static string Plain(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            // Very small helper to strip HTML tags for logs
            return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", " ")
                .Trim();
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
