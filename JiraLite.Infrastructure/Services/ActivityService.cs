using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Common;
using JiraLite.Application.Exceptions;
using JiraLite.Application.Interfaces;
using JiraLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JiraLite.Infrastructure.Services
{
    public class ActivityService : IActivityService
    {
        private readonly JiraLiteDbContext _db;

        public ActivityService(JiraLiteDbContext db)
        {
            _db = db;
        }

        public async Task LogAsync(Guid projectId, Guid? taskId, Guid actorId, string actionType, string message)
        {
            var item = new JiraLite.Domain.Entities.ActivityLog
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
                .Select(a => new ActivityLogDto
                {
                    Id = a.Id,
                    ProjectId = a.ProjectId,
                    TaskId = a.TaskId,
                    ActorId = a.ActorId,
                    ActionType = a.ActionType,
                    Message = a.Message,
                    CreatedAt = a.CreatedAt
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
                .Select(a => new ActivityLogDto
                {
                    Id = a.Id,
                    ProjectId = a.ProjectId,
                    TaskId = a.TaskId,
                    ActorId = a.ActorId,
                    ActionType = a.ActionType,
                    Message = a.Message,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<ActivityLogDto>(items, query.Page, query.PageSize, total);
        }

        private static IQueryable<JiraLite.Domain.Entities.ActivityLog> ApplyFilters(
            IQueryable<JiraLite.Domain.Entities.ActivityLog> q,
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
    }
}