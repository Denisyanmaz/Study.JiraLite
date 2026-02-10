using JiraLite.Application.DTOs;
using JiraLite.Application.Interfaces;
using JiraLite.Domain.Entities;
using JiraLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using JiraLite.Application.Exceptions;

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
    }
}
