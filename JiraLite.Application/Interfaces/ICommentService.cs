using JiraLite.Application.DTOs.Comment;

namespace JiraLite.Application.Interfaces
{
    public interface ICommentService
    {
        Task<CommentDto> AddToTaskAsync(Guid taskId, CreateCommentDto dto, Guid currentUserId);
        Task<List<CommentDto>> GetByTaskAsync(Guid taskId, Guid currentUserId);
    }
}
