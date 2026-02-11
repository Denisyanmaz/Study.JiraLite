using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Common;

namespace JiraLite.Application.Interfaces
{
    public interface IActivityService
    {
        Task<PagedResult<ActivityLogDto>> GetByProjectAsync(Guid projectId, ActivityFilterQueryDto query, Guid currentUserId);
        Task<PagedResult<ActivityLogDto>> GetByTaskAsync(Guid taskId, ActivityFilterQueryDto query, Guid currentUserId);

        Task LogAsync(Guid projectId, Guid? taskId, Guid actorId, string actionType, string message);
    }
}
