using DenoLite.Application.DTOs.BoardColumn;
using DenoLite.Application.Exceptions;
using DenoLite.Application.Interfaces;
using DenoLite.Domain.Entities;
using DenoLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DenoLite.Infrastructure.Services
{
    public class BoardColumnService : IBoardColumnService
    {
        private const int MaxColumnsPerProject = 5;
        private readonly DenoLiteDbContext _db;

        public BoardColumnService(DenoLiteDbContext db)
        {
            _db = db;
        }

        public async Task<List<BoardColumnDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId)
        {
            await EnsureMemberAsync(projectId, currentUserId);

            var columns = await _db.BoardColumns
                .Where(bc => bc.ProjectId == projectId)
                .OrderBy(bc => bc.SortOrder)
                .Select(bc => new BoardColumnDto
                {
                    Id = bc.Id,
                    ProjectId = bc.ProjectId,
                    Name = bc.Name,
                    SortOrder = bc.SortOrder,
                    TaskCount = _db.Tasks.Count(t => t.BoardColumnId == bc.Id)
                })
                .ToListAsync();

            return columns;
        }

        public async Task<BoardColumnDto> CreateAsync(Guid projectId, CreateBoardColumnDto dto, Guid currentUserId)
        {
            await EnsureMemberAsync(projectId, currentUserId);

            var count = await _db.BoardColumns.CountAsync(bc => bc.ProjectId == projectId);
            if (count >= MaxColumnsPerProject)
                throw new BadRequestException($"A project can have at most {MaxColumnsPerProject} board columns. Remove or merge a column first.");

            var name = (dto.Name ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                throw new BadRequestException("Column name is required.");

            var maxOrder = await _db.BoardColumns
                .Where(bc => bc.ProjectId == projectId)
                .MaxAsync(bc => (int?)bc.SortOrder) ?? -1;

            var column = new BoardColumn
            {
                ProjectId = projectId,
                Name = name,
                SortOrder = maxOrder + 1,
                CreatedBy = currentUserId
            };
            _db.BoardColumns.Add(column);
            await _db.SaveChangesAsync();

            return new BoardColumnDto
            {
                Id = column.Id,
                ProjectId = column.ProjectId,
                Name = column.Name,
                SortOrder = column.SortOrder,
                TaskCount = 0
            };
        }

        public async Task<BoardColumnDto> UpdateAsync(Guid columnId, UpdateBoardColumnDto dto, Guid currentUserId)
        {
            var column = await _db.BoardColumns
                .FirstOrDefaultAsync(bc => bc.Id == columnId);
            if (column == null)
                throw new KeyNotFoundException("Board column not found.");

            await EnsureMemberAsync(column.ProjectId, currentUserId);

            var name = (dto.Name ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                throw new BadRequestException("Column name is required.");

            column.Name = name;
            await _db.SaveChangesAsync();

            var taskCount = await _db.Tasks.CountAsync(t => t.BoardColumnId == column.Id);
            return new BoardColumnDto
            {
                Id = column.Id,
                ProjectId = column.ProjectId,
                Name = column.Name,
                SortOrder = column.SortOrder,
                TaskCount = taskCount
            };
        }

        public async Task DeleteAsync(Guid columnId, Guid currentUserId)
        {
            var column = await _db.BoardColumns
                .FirstOrDefaultAsync(bc => bc.Id == columnId);
            if (column == null)
                throw new KeyNotFoundException("Board column not found.");

            await EnsureMemberAsync(column.ProjectId, currentUserId);

            var taskCount = await _db.Tasks.CountAsync(t => t.BoardColumnId == columnId);
            if (taskCount > 0)
                throw new ConflictException(
                    $"This column has {taskCount} task(s). Move them to other columns first to avoid losing them. You cannot delete a column that still contains tasks.");

            _db.BoardColumns.Remove(column);
            await _db.SaveChangesAsync();
        }

        private async Task EnsureMemberAsync(Guid projectId, Guid userId)
        {
            var isMember = await _db.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");
        }
    }
}
