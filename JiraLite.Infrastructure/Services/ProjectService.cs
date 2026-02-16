using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Common;
using JiraLite.Application.Exceptions;
using JiraLite.Application.Interfaces;
using JiraLite.Domain.Entities;
using JiraLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JiraLite.Infrastructure.Services
{
    public class ProjectService : IProjectService
    {
        private readonly JiraLiteDbContext _db;

        public ProjectService(JiraLiteDbContext db)
        {
            _db = db;
        }

        public async Task<bool> IsOwnerAsync(Guid projectId, Guid userId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new KeyNotFoundException("Project not found");

            return project.OwnerId == userId;
        }

        public async Task<bool> IsMemberAsync(Guid projectId, Guid userId)
        {
            return await _db.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        }

        public async Task<ProjectDto> CreateAsync(Guid userId, CreateProjectDto dto)
        {
            var project = new Project
            {
                Name = dto.Name,
                Description = dto.Description,
                OwnerId = userId
            };

            _db.Projects.Add(project);

            _db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = userId,
                Role = "Owner"
            });

            await _db.SaveChangesAsync();

            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                OwnerId = project.OwnerId
            };
        }

        public async Task<List<ProjectDto>> GetMyProjectsAsync(Guid userId)
        {
            return await _db.ProjectMembers
                .Where(pm => pm.UserId == userId)
                .Select(pm => new ProjectDto
                {
                    Id = pm.Project.Id,
                    Name = pm.Project.Name,
                    Description = pm.Project.Description,
                    OwnerId = pm.Project.OwnerId
                })
                .ToListAsync();
        }

        public async Task<ProjectMemberDto> AddMemberAsync(Guid projectId, ProjectMemberDto dto, Guid currentUserId)
        {
            // Ensure project exists (so you get 404 not 500)
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId);
            if (!projectExists)
                throw new KeyNotFoundException("Project not found");

            // Owner-only
            var currentMember = await _db.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId);

            if (currentMember == null || currentMember.Role != "Owner")
                throw new ForbiddenException("Only project owners can add members.");

            // Prevent duplicates -> 409
            var exists = await _db.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == dto.UserId);

            if (exists)
                throw new ConflictException("User is already a member of this project.");

            var member = new ProjectMember
            {
                ProjectId = projectId,
                UserId = dto.UserId,
                Role = dto.Role
            };

            _db.ProjectMembers.Add(member);
            await _db.SaveChangesAsync();

            return new ProjectMemberDto
            {
                UserId = member.UserId,
                Role = member.Role
            };
        }
        public async Task<List<ProjectMemberDto>> GetMembersAsync(Guid projectId, Guid currentUserId)
        {
            // Ensure project exists (404)
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId);
            if (!projectExists)
                throw new KeyNotFoundException("Project not found");

            // Must be a member to view members
            var isMember = await _db.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId);

            if (!isMember)
                throw new ForbiddenException("You are not a member of this project.");

            // Return members
            return await _db.ProjectMembers
                .Where(pm => pm.ProjectId == projectId)
                .OrderByDescending(pm => pm.Role == "Owner") // owners first
                .ThenBy(pm => pm.UserId)
                .Select(pm => new ProjectMemberDto
                {
                    UserId = pm.UserId,
                    Role = pm.Role
                })
                .ToListAsync();
        }
        public async Task RemoveMemberAsync(Guid projectId, Guid memberUserId, Guid currentUserId)
        {
            // Ensure project exists (404)
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new KeyNotFoundException("Project not found");

            // Must be a member at least
            var currentMember = await _db.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId);

            if (currentMember == null)
                throw new ForbiddenException("You are not a member of this project.");

            // If current user is NOT owner -> can only remove himself (leave project)
            if (!string.Equals(currentMember.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                if (memberUserId != currentUserId)
                    throw new ForbiddenException("Members can only remove themselves (leave the project).");

                // Member leaving: remove their membership record
                var self = await _db.ProjectMembers
                    .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId);

                if (self == null)
                    throw new KeyNotFoundException("Member not found");

                _db.ProjectMembers.Remove(self);
                await _db.SaveChangesAsync();
                return;
            }

            // Owner flow: can remove other members, but cannot remove the project owner
            if (memberUserId == project.OwnerId)
                throw new ConflictException("You cannot remove the project owner.");

            var member = await _db.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == memberUserId);

            if (member == null)
                throw new KeyNotFoundException("Member not found");

            _db.ProjectMembers.Remove(member);
            await _db.SaveChangesAsync();
        }

        public async Task<PagedResult<ProjectDto>> GetMyProjectsPagedAsync(Guid userId, int page, int pageSize)
        {
            var query = _db.ProjectMembers
                .Where(pm => pm.UserId == userId)
                .Select(pm => new ProjectDto
                {
                    Id = pm.Project.Id,
                    Name = pm.Project.Name,
                    Description = pm.Project.Description,
                    OwnerId = pm.Project.OwnerId
                });

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(p => p.Name) // deterministic ordering for paging (important for tests)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<ProjectDto>(items, page, pageSize, total);
        }
    }
}
