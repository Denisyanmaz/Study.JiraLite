using JiraLite.Application.DTOs.Common;
using JiraLite.Application.DTOs.Task;
using JiraLite.Domain.Entities;
using JiraLite.Domain.Enums;

namespace JiraLite.Application.Interfaces
{
    public interface ITaskService
    {
        // Current user ID added to enforce permissions
        Task<TaskItem> CreateTaskAsync(TaskItemDto dto, Guid currentUserId);
        Task<TaskItem?> GetTaskByIdAsync(Guid taskId, Guid currentUserId);
        Task<List<TaskItem>> GetTasksByProjectAsync(Guid projectId, Guid currentUserId);
        Task<PagedResult<TaskItem>> GetTasksByProjectPagedAsync(Guid projectId, Guid currentUserId, TaskQueryDto query);
        Task<TaskItem> UpdateTaskAsync(Guid taskId, TaskItemDto dto, Guid currentUserId);
        Task<TaskItem> UpdateTaskStatusAsync(Guid taskId, JiraTaskStatus status, Guid currentUserId);
        Task DeleteTaskAsync(Guid taskId, Guid currentUserId);
    }
}
