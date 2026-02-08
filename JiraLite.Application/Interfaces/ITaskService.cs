using JiraLite.Application.DTOs;
using JiraLite.Domain.Entities;

namespace JiraLite.Application.Interfaces
{
    public interface ITaskService
    {
        // Current user ID added to enforce permissions
        Task<TaskItem> CreateTaskAsync(TaskItemDto dto, Guid currentUserId);
        Task<TaskItem?> GetTaskByIdAsync(Guid taskId, Guid currentUserId);
        Task<List<TaskItem>> GetTasksByProjectAsync(Guid projectId, Guid currentUserId);
        Task<TaskItem> UpdateTaskAsync(Guid taskId, TaskItemDto dto, Guid currentUserId);
        Task DeleteTaskAsync(Guid taskId, Guid currentUserId);
    }
}
