using DenoLite.Application.DTOs.BoardColumn;

namespace DenoLite.Application.Interfaces
{
    public interface IBoardColumnService
    {
        Task<List<BoardColumnDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId);
        Task<BoardColumnDto> CreateAsync(Guid projectId, CreateBoardColumnDto dto, Guid currentUserId);
        Task<BoardColumnDto> UpdateAsync(Guid columnId, UpdateBoardColumnDto dto, Guid currentUserId);
        Task DeleteAsync(Guid columnId, Guid currentUserId);
    }
}
