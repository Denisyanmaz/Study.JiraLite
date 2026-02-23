using DenoLite.Application.DTOs.Comment;
using DenoLite.Application.Exceptions;
using DenoLite.Application.Interfaces;
using DenoLite.Domain.Entities;
using DenoLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DenoLite.Infrastructure.Services
{
    public class CommentService : ICommentService
    {
        private readonly DenoLiteDbContext _db;
        private readonly IActivityService _activity;

        public CommentService(DenoLiteDbContext db, IActivityService activity)
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
                message: $"Comment added on task '{task.Title}': \"{(dto.Body ?? "").Trim()}\""
            );

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
    }
}
