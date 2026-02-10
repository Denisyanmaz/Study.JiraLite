using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Common;

namespace JiraLite.Application.Interfaces
{
    public interface IActivityService
    {
        Task<PagedResult<ActivityLogDto>> GetByProjectAsync(Guid projectId, ActivityPagedQueryDto paging, Guid currentUserId);
        Task<PagedResult<ActivityLogDto>> GetByTaskAsync(Guid taskId, ActivityPagedQueryDto paging, Guid currentUserId);

        // internal use by other services
        Task LogAsync(Guid projectId, Guid? taskId, Guid actorId, string actionType, string message);
    }
}
