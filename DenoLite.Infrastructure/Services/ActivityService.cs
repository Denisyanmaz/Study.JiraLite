using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.Common;
using DenoLite.Application.Exceptions;
using DenoLite.Application.Interfaces;
using DenoLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DenoLite.Infrastructure.Services
{
    public class ActivityService : IActivityService
    {
        private readonly DenoLiteDbContext _db;

        public ActivityService(DenoLiteDbContext db)
        {
            _db = db;
        }

        public async Task LogAsync(Guid projectId, Guid? taskId, Guid actorId, string actionType, string message)
        {
            var item = new DenoLite.Domain.Entities.ActivityLog
            {
                ProjectId = projectId,
                TaskId = taskId,
                ActorId = actorId,
                ActionType = actionType,
                Message = message
            };

            _db.ActivityLogs.Add(item);
            await _db.SaveChangesAsync();
        }

        public async Task<PagedResult<ActivityLogDto>> GetByProjectAsync(
            Guid projectId,
            ActivityFilterQueryDto query,
            Guid currentUserId)
        {
            // 🔐 Membership check
            var isMember = await _db.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId);

            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            var q = _db.ActivityLogs.Where(a => a.ProjectId == projectId);

            q = ApplyFilters(q, query);

            var total = await q.CountAsync();

            var items = await q
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .GroupJoin(
                    _db.Tasks,
                    a => a.TaskId,
                    t => (Guid?)t.Id,
                    (a, tasks) => new { Activity = a, Task = tasks.FirstOrDefault() }
                )
                .Select(x => new ActivityLogDto
                {
                    Id = x.Activity.Id,
                    ProjectId = x.Activity.ProjectId,
                    TaskId = x.Activity.TaskId,
                    TaskTitle = x.Task != null ? x.Task.Title : null,
                    ActorId = x.Activity.ActorId,
                    ActionType = x.Activity.ActionType,
                    Message = x.Activity.Message,
                    CreatedAt = x.Activity.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<ActivityLogDto>(items, query.Page, query.PageSize, total);
        }

        public async Task<PagedResult<ActivityLogDto>> GetByTaskAsync(
            Guid taskId,
            ActivityFilterQueryDto query,
            Guid currentUserId)
        {
            // Task is soft-delete filtered; if deleted -> null -> 404
            var task = await _db.Tasks.FindAsync(taskId);
            if (task == null)
                throw new NotFoundException("Task not found");

            // 🔐 Membership check
            var isMember = await _db.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == task.ProjectId && pm.UserId == currentUserId);

            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            var q = _db.ActivityLogs.Where(a => a.TaskId == taskId);

            q = ApplyFilters(q, query);

            var total = await q.CountAsync();

            var items = await q
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .GroupJoin(
                    _db.Tasks,
                    a => a.TaskId,
                    t => (Guid?)t.Id,
                    (a, tasks) => new { Activity = a, Task = tasks.FirstOrDefault() }
                )
                .Select(x => new ActivityLogDto
                {
                    Id = x.Activity.Id,
                    ProjectId = x.Activity.ProjectId,
                    TaskId = x.Activity.TaskId,
                    TaskTitle = x.Task != null ? x.Task.Title : null,
                    ActorId = x.Activity.ActorId,
                    ActionType = x.Activity.ActionType,
                    Message = x.Activity.Message,
                    CreatedAt = x.Activity.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<ActivityLogDto>(items, query.Page, query.PageSize, total);
        }

        private static IQueryable<DenoLite.Domain.Entities.ActivityLog> ApplyFilters(
            IQueryable<DenoLite.Domain.Entities.ActivityLog> q,
            ActivityFilterQueryDto query)
        {
            if (!string.IsNullOrWhiteSpace(query.ActionType))
                q = q.Where(a => a.ActionType == query.ActionType);

            if (query.TaskId.HasValue)
                q = q.Where(a => a.TaskId == query.TaskId);

            if (query.ActorId.HasValue)
                q = q.Where(a => a.ActorId == query.ActorId);

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var text = query.Q.Trim();

                // ✅ PostgreSQL-friendly LIKE (case-insensitive)
                // If you use Npgsql, this works well:
                q = q.Where(a =>
                    EF.Functions.ILike(a.Message, $"%{text}%") ||
                    EF.Functions.ILike(a.ActionType, $"%{text}%"));
            }

            // safety defaults
            if (query.Page < 1) query.Page = 1;
            if (query.PageSize < 1) query.PageSize = 20;
            if (query.PageSize > 200) query.PageSize = 200;

            return q;
        }

        /// <summary>One-time fix: update existing CommentAdded activity messages to use full comment body.</summary>
        public async Task<int> FixCommentAddedMessagesAsync()
        {
            var activities = await _db.ActivityLogs
                .Where(a => a.ActionType == "CommentAdded" && a.TaskId != null)
                .ToListAsync();

            int updated = 0;
            foreach (var activity in activities)
            {
                var taskId = activity.TaskId!.Value;
                var task = await _db.Tasks.FindAsync(taskId);
                if (task == null) continue;

                var comments = await _db.Comments
                    .Where(c => c.TaskId == taskId && c.AuthorId == activity.ActorId)
                    .ToListAsync();
                var comment = comments
                    .OrderBy(c => Math.Abs((c.CreatedAt - activity.CreatedAt).Ticks))
                    .FirstOrDefault();

                if (comment == null) continue;

                var newMessage = $"Comment added on task '{task.Title}': \"{(comment.Body ?? "").Trim()}\"";
                if (activity.Message != newMessage)
                {
                    activity.Message = newMessage;
                    updated++;
                }
            }

            if (updated > 0)
                await _db.SaveChangesAsync();

            return updated;
        }
    }
}