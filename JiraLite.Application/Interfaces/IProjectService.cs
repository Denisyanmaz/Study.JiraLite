using JiraLite.Application.DTOs.Common;
using JiraLite.Application.DTOs.Project;
using JiraLite.Application.DTOs.ProjectMember;

namespace JiraLite.Application.Interfaces
{
    public interface IProjectService
    {
        Task<ProjectDto> CreateAsync(Guid userId, CreateProjectDto dto);
        Task<List<ProjectDto>> GetMyProjectsAsync(Guid userId);

        // Step 6.2: Project members / role enforcement
        Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberDto dto, Guid currentUserId);
        Task<bool> IsOwnerAsync(Guid projectId, Guid userId);
        Task<bool> IsMemberAsync(Guid projectId, Guid userId);
        Task<PagedResult<ProjectDto>> GetMyProjectsPagedAsync(Guid userId, int page, int pageSize);
        Task<List<ProjectMemberDto>> GetMembersAsync(Guid projectId, Guid currentUserId);
        Task RemoveMemberAsync(Guid projectId, Guid memberUserId, Guid currentUserId);

    }
}
