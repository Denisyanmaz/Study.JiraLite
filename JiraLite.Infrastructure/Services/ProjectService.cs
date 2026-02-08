using JiraLite.Application.DTOs;
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

        // =========================
        // Check if user is project owner
        // =========================
        public async Task<bool> IsOwnerAsync(Guid projectId, Guid userId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) throw new Exception("Project not found");
            return project.OwnerId == userId;
        }

        // =========================
        // Check if user is project member
        // =========================
        public async Task<bool> IsMemberAsync(Guid projectId, Guid userId)
        {
            return await _db.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        }

        // =========================
        // Create Project
        // =========================
        public async Task<ProjectDto> CreateAsync(Guid userId, CreateProjectDto dto)
        {
            var project = new Project
            {
                Name = dto.Name,
                Description = dto.Description,
                OwnerId = userId
            };

            _db.Projects.Add(project);

            // Owner is automatically a project member
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

        // =========================
        // Get My Projects
        // =========================
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

        // =========================
        // Add Project Member (Owner only)
        // =========================
        public async Task<ProjectMemberDto> AddMemberAsync(
                    Guid projectId,
                    ProjectMemberDto dto,
                    Guid currentUserId)
        {
            // 1️⃣ Ensure current user is project Owner
            var currentMember = await _db.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId);

            if (currentMember == null || currentMember.Role != "Owner")
                throw new UnauthorizedAccessException("Only project owners can add members.");

            // 2️⃣ Prevent duplicates
            var exists = await _db.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == dto.UserId);

            if (exists)
                throw new Exception("User is already a member of this project.");

            // 3️⃣ Add new member
            var member = new ProjectMember
            {
                ProjectId = projectId,
                UserId = dto.UserId,
                Role = dto.Role
            };

            _db.ProjectMembers.Add(member);
            await _db.SaveChangesAsync();

            // 4️⃣ Return DTO (UserId and Role only)
            return new ProjectMemberDto
            {
                UserId = member.UserId,
                Role = member.Role
            };
        }

    }
}
