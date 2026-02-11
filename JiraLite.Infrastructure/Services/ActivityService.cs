using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Common;
using JiraLite.Application.Exceptions;
using JiraLite.Application.Interfaces;
using JiraLite.Domain.Entities;
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
            var item = new ActivityLog
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
            var isMember = await _db.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId);

            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            IQueryable<ActivityLog> logs = _db.ActivityLogs.Where(a => a.ProjectId == projectId);

            // ✅ filters
            if (!string.IsNullOrWhiteSpace(query.ActionType))
                logs = logs.Where(a => a.ActionType == query.ActionType);

            if (query.TaskId.HasValue)
                logs = logs.Where(a => a.TaskId == query.TaskId.Value);

            if (query.ActorId.HasValue)
                logs = logs.Where(a => a.ActorId == query.ActorId.Value);

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var q = query.Q.Trim();

                logs = logs.Where(a =>
                    EF.Functions.ILike(a.Message, $"%{q}%") ||
                    EF.Functions.ILike(a.ActionType, $"%{q}%"));
            }

            var total = await logs.CountAsync();

            var items = await logs
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
            var task = await _db.Tasks.FindAsync(taskId);
            if (task == null)
                throw new NotFoundException("Task not found");

            var isMember = await _db.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == task.ProjectId && pm.UserId == currentUserId);

            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            IQueryable<ActivityLog> logs = _db.ActivityLogs.Where(a => a.TaskId == taskId);

            // ✅ filters
            if (!string.IsNullOrWhiteSpace(query.ActionType))
                logs = logs.Where(a => a.ActionType == query.ActionType);

            // TaskId filter is redundant here, but harmless; ignore it for clarity
            if (query.ActorId.HasValue)
                logs = logs.Where(a => a.ActorId == query.ActorId.Value);

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var q = query.Q.Trim();

                logs = logs.Where(a =>
                    EF.Functions.ILike(a.Message, $"%{q}%") ||
                    EF.Functions.ILike(a.ActionType, $"%{q}%"));
            }


            var total = await logs.CountAsync();

            var items = await logs
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
    }
}
