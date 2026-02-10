using JiraLite.Application.DTOs;
using JiraLite.Application.Exceptions;
using JiraLite.Application.Interfaces;
using JiraLite.Domain.Entities;
using JiraLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JiraLite.Infrastructure.Services
{
    public class CommentService : ICommentService
    {
        private readonly JiraLiteDbContext _db;
        private readonly IActivityService _activity;

        public CommentService(JiraLiteDbContext db, IActivityService activity)
        {
            _db = db;
            _activity = activity;
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

            var comment = new TaskComment
            {
                TaskId = taskId,
                AuthorId = currentUserId,
                Body = dto.Body
                // CreatedBy is filled by auditing
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            await _activity.LogAsync(
                projectId: task.ProjectId,
                taskId: task.Id,
                actorId: currentUserId,
                actionType: "CommentAdded",
                message: "A comment was added."
            );

            return new CommentDto
            {
                Id = comment.Id,
                TaskId = comment.TaskId,
                AuthorId = comment.AuthorId,
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
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    TaskId = c.TaskId,
                    AuthorId = c.AuthorId,
                    Body = c.Body,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();
        }
    }
}
