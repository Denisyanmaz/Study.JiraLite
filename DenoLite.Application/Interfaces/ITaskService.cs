using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.Task;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;

namespace DenoLite.Application.Interfaces
{
    public interface ITaskService
    {
        // Current user ID added to enforce permissions
        Task<TaskItem> CreateTaskAsync(TaskItemDto dto, Guid currentUserId);
        Task<TaskItem?> GetTaskByIdAsync(Guid taskId, Guid currentUserId);
        Task<List<TaskItem>> GetTasksByProjectAsync(Guid projectId, Guid currentUserId);
        Task<PagedResult<TaskItem>> GetTasksByProjectPagedAsync(Guid projectId, Guid currentUserId, TaskQueryDto query);
        Task<TaskItem> UpdateTaskAsync(Guid taskId, TaskItemDto dto, Guid currentUserId);
        Task<TaskItem> UpdateTaskStatusAsync(Guid taskId, DenoTaskStatus status, Guid currentUserId);
        Task DeleteTaskAsync(Guid taskId, Guid currentUserId);
    }
}
