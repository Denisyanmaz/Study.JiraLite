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
            ActivityPagedQueryDto paging,
            Guid currentUserId)
        {
            // 🔐 Membership check
            var isMember = await _db.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId);

            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            var query = _db.ActivityLogs
                .Where(a => a.ProjectId == projectId);

            var total = await query.CountAsync();

            // stable ordering (CreatedAt desc, then Id desc)
            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Skip((paging.Page - 1) * paging.PageSize)
                .Take(paging.PageSize)
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

            // ✅ IMPORTANT: your PagedResult requires constructor args
            return new PagedResult<ActivityLogDto>(items, paging.Page, paging.PageSize, total);
        }

        public async Task<PagedResult<ActivityLogDto>> GetByTaskAsync(
            Guid taskId,
            ActivityPagedQueryDto paging,
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

            var query = _db.ActivityLogs
                .Where(a => a.TaskId == taskId);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Skip((paging.Page - 1) * paging.PageSize)
                .Take(paging.PageSize)
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

            return new PagedResult<ActivityLogDto>(items, paging.Page, paging.PageSize, total);
        }
    }
}
