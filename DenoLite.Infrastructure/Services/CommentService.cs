using DenoLite.Application.DTOs.Comment;
using DenoLite.Application.Exceptions;
using DenoLite.Application.Interfaces;
using DenoLite.Domain.Entities;
using DenoLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace DenoLite.Infrastructure.Services
{
    public class CommentService : ICommentService
    {
        private readonly DenoLiteDbContext _db;
        private readonly IActivityService _activity;
        private readonly IEmailSender _emailSender;

        public CommentService(DenoLiteDbContext db, IActivityService activity, IEmailSender emailSender)
        {
            _db = db;
            _activity = activity;
            _emailSender = emailSender;
        }

        public async Task<CommentDto> AddToTaskAsync(Guid taskId, CreateCommentDto dto, Guid currentUserId)
        {
            // Soft delete filter applies here: deleted task => null => 404
            var task = await _db.Tasks.FindAsync(taskId);
            if (task == null)
                throw new NotFoundException("Task not found");

            var isMember = await _db.ProjectMembers.AnyAsync(pm => pm.ProjectId == task.ProjectId && pm.UserId == currentUserId);
            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            // Resolve assignee email up-front (before any background email tasks)
            var assigneeEmail = await _db.Users
                .Where(u => u.Id == task.AssigneeId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            var comment = new TaskComment
            {
                TaskId = taskId,
                AuthorId = currentUserId,
                Body = dto.Body
                // CreatedBy is filled by auditing
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            var preview = Plain(comment.Body);

            await _activity.LogAsync(
                projectId: task.ProjectId,
                taskId: task.Id,
                actorId: currentUserId,
                actionType: "CommentAdded",
                message: $"Comment added on task '{task.Title}': \"{preview}\""
            );

            // Best-effort mention notifications for comments (reuse same handle logic as tasks)
            await NotifyMentionedUsersAsync(task, comment, currentUserId);

            // Notify assignee about the new comment.
            // Skip if the author is the assignee themselves.
            if (task.AssigneeId != Guid.Empty &&
                task.AssigneeId != currentUserId &&
                !string.IsNullOrWhiteSpace(assigneeEmail))
            {
                _ = SendNewCommentEmailAsync(assigneeEmail, task, comment);
            }

            var author = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId);
            return new CommentDto
            {
                Id = comment.Id,
                TaskId = comment.TaskId,
                AuthorId = comment.AuthorId,
                AuthorEmail = author?.Email,
                Body = comment.Body,
                CreatedAt = comment.CreatedAt
            };
        }

        public async Task<List<CommentDto>> GetByTaskAsync(Guid taskId, Guid currentUserId)
        {
            var task = await _db.Tasks.FindAsync(taskId);
            if (task == null)
                throw new NotFoundException("Task not found");

            var isMember = await _db.ProjectMembers.AnyAsync(pm => pm.ProjectId == task.ProjectId && pm.UserId == currentUserId);
            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            return await _db.Comments
                .Where(c => c.TaskId == taskId)
                .OrderBy(c => c.CreatedAt)
                .Join(_db.Users, c => c.AuthorId, u => u.Id, (c, u) => new { c, u })
                .Select(x => new CommentDto
                {
                    Id = x.c.Id,
                    TaskId = x.c.TaskId,
                    AuthorId = x.c.AuthorId,
                    AuthorEmail = x.u.Email,
                    Body = x.c.Body,
                    CreatedAt = x.c.CreatedAt
                })
                .ToListAsync();
        }

        private async Task NotifyMentionedUsersAsync(TaskItem task, TaskComment comment, Guid actorId)
        {
            if (string.IsNullOrWhiteSpace(comment.Body))
                return;

            var handles = ExtractMentionHandles(comment.Body);
            if (handles.Count == 0)
                return;

            try
            {
                var project = await _db.Projects.FindAsync(task.ProjectId);
                var projectName = project?.Name ?? string.Empty;

                var members = await (
                    from pm in _db.ProjectMembers
                    join u in _db.Users on pm.UserId equals u.Id
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
                        targets[m.UserId] = m.Email;
                    }
                }

                if (targets.Count == 0)
                    return;

                foreach (var (_, email) in targets)
                {
                    _ = SendCommentMentionEmailAsync(email, task, comment, projectName);
                }
            }
            catch
            {
                // Mention failures must not break comment flow
            }
        }

        private static List<string> ExtractMentionHandles(string? text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return result;

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

        private static string Plain(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            var text = Regex.Replace(html, "<.*?>", string.Empty);
            text = text.Replace("\r", string.Empty).Replace("\n", " ");
            return text.Trim();
        }

        private async Task SendCommentMentionEmailAsync(string email, TaskItem task, TaskComment comment, string? projectName)
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
                    ? $"You were mentioned in a comment on \"{safeTitle}\""
                    : $"You were mentioned in a comment on \"{safeTitle}\" ({safeProject})";

                var bodyBuilder = new System.Text.StringBuilder();
                bodyBuilder.Append("<p>Hi,</p>");
                bodyBuilder.Append("<p>You were mentioned in a comment in <strong>DenoLite</strong>");
                if (!string.IsNullOrWhiteSpace(safeProject))
                {
                    bodyBuilder.Append($" (project <strong>{safeProject}</strong>)");
                }
                bodyBuilder.Append(":</p>");

                bodyBuilder.Append($"<p><strong>{safeTitle}</strong></p>");

                if (!string.IsNullOrWhiteSpace(comment.Body))
                {
                    bodyBuilder.Append("<hr/>");
                    bodyBuilder.Append("<div>");
                    bodyBuilder.Append(comment.Body);
                    bodyBuilder.Append("</div>");
                }

                bodyBuilder.Append(
                    $@"<p><a href=""/Tasks/Details/{task.Id}?tab=comments"">View task comments</a></p>");
                bodyBuilder.Append("<p>— The DenoLite Team</p>");

                await _emailSender.SendAsync(email, subject, bodyBuilder.ToString());
            }
            catch
            {
                // Email failures must not break comment flow
            }
        }

        private async Task SendNewCommentEmailAsync(string assigneeEmail, TaskItem task, TaskComment comment)
        {
            try
            {
                var subject = $"New comment on task \"{task.Title}\"";
                var safeTitle = System.Net.WebUtility.HtmlEncode(task.Title ?? string.Empty);
                var bodyBuilder = new System.Text.StringBuilder();
                bodyBuilder.Append("<p>Hi,</p>");
                bodyBuilder.Append("<p>There is a new comment on your task in <strong>DenoLite</strong>:</p>");
                bodyBuilder.Append($"<p><strong>{safeTitle}</strong></p>");

                if (!string.IsNullOrWhiteSpace(comment.Body))
                {
                    bodyBuilder.Append("<hr/>");
                    bodyBuilder.Append("<div>");
                    bodyBuilder.Append(comment.Body);
                    bodyBuilder.Append("</div>");
                }

                bodyBuilder.Append(
                    $@"<p><a href=""/Tasks/Details/{task.Id}?tab=comments"">View task comments</a></p>");
                bodyBuilder.Append("<p>— The DenoLite Team</p>");

                await _emailSender.SendAsync(assigneeEmail, subject, bodyBuilder.ToString());
            }
            catch
            {
                // Comment notification failures must not break comment flow
            }
        }
    }
}
