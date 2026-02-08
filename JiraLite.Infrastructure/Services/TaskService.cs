using JiraLite.Application.DTOs;
using JiraLite.Application.Interfaces;
using JiraLite.Application.Exceptions;
using JiraLite.Domain.Entities;
using JiraLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JiraLite.Infrastructure.Services
{
    public class TaskService : ITaskService
    {
        private readonly JiraLiteDbContext _context;

        public TaskService(JiraLiteDbContext context)
        {
            _context = context;
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
            if (task == null) throw new Exception("Task not found");

            var project = await _context.Projects.FindAsync(task.ProjectId);
            if (project == null) throw new Exception("Project not found");

            if (task.AssigneeId != currentUserId && project.OwnerId != currentUserId)
                throw new ForbiddenException("Only the assignee or project owner can update this task.");

            task.Title = dto.Title;
            task.Description = dto.Description;
            task.Status = dto.Status;
            task.Priority = dto.Priority;
            task.AssigneeId = dto.AssigneeId;
            task.DueDate = dto.DueDate;

            await _context.SaveChangesAsync();
            return task;
        }

        // 🔹 Delete Task (only assignee or project owner)
        public async Task DeleteTaskAsync(Guid taskId, Guid currentUserId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null) throw new Exception("Task not found");

            var project = await _context.Projects.FindAsync(task.ProjectId);
            if (project == null) throw new Exception("Project not found");

            if (task.AssigneeId != currentUserId && project.OwnerId != currentUserId)
                throw new ForbiddenException("Only the assignee or project owner can delete this task.");

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }
}
