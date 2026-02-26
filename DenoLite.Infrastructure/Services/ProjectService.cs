using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.Project;
using DenoLite.Application.DTOs.ProjectMember;
using DenoLite.Application.Exceptions;
using DenoLite.Application.Interfaces;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DenoLite.Infrastructure.Services
{
    public class ProjectService : IProjectService
    {
        private readonly DenoLiteDbContext _db;
        private readonly IActivityService _activity;
        private readonly IEmailSender _emailSender;

        public ProjectService(DenoLiteDbContext db, IActivityService activity, IEmailSender emailSender)
        {
            _db = db;
            _activity = activity;
            _emailSender = emailSender;
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
            // Verify user exists before creating project
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                throw new BadRequestException("User not found. Cannot create project for non-existent user.");

            var name = (dto.Name ?? "").Trim();
            if (await _db.Projects.AnyAsync(p => p.OwnerId == userId && p.Name == name))
                throw new ConflictException("A project with this name already exists. Please choose a different name.");

            var project = new Project
            {
                Name = name,
                Description = dto.Description?.Trim(),
                OwnerId = userId
            };

            _db.Projects.Add(project);

            // Use navigation property so EF Core handles the relationship correctly
            project.Members.Add(new ProjectMember
            {
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

        public async Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberDto dto, Guid currentUserId)
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
            // Rule: owner cannot add another Owner via AddMember

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var email = await _db.Users
                    .Where(u => u.Id == dto.UserId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync();

                var member = new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = dto.UserId,
                    Role = "Member"
                };
                _db.ProjectMembers.Add(member);

                // Only create welcome task if they don't already have a task in this project (e.g. re-added member).
                var alreadyHasTaskInProject = await _db.Tasks
                    .AnyAsync(t => t.ProjectId == projectId && t.AssigneeId == dto.UserId && !t.IsDeleted);

                TaskItem? welcomeTask = null;
                if (!alreadyHasTaskInProject)
                {
                    var taskTitle = string.IsNullOrEmpty(email) ? "New member" : (email.IndexOf('@') > 0 ? email[..email.IndexOf('@')] : email);
                    welcomeTask = new TaskItem
                    {
                        Title = taskTitle,
                        Description = "read the project details",
                        Status = DenoTaskStatus.Todo,
                        Priority = 1,
                        AssigneeId = dto.UserId,
                        ProjectId = projectId,
                        DueDate = DateTime.UtcNow.AddDays(3),
                        CreatedBy = currentUserId
                    };
                    _db.Tasks.Add(welcomeTask);
                }

                await _db.SaveChangesAsync();

                await _activity.LogAsync(
                    projectId: projectId,
                    taskId: null,
                    actorId: currentUserId,
                    actionType: "MemberAdded",
                    message: $"Member added: {email ?? "Unknown"}"
                );

                await transaction.CommitAsync();

                if (welcomeTask != null)
                    _ = SendAssignmentEmailAsync(welcomeTask.AssigneeId, welcomeTask.Title, welcomeTask.Id);

                return new ProjectMemberDto
                {
                    UserId = member.UserId,
                    Role = member.Role,
                    Email = email
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task SendAssignmentEmailAsync(Guid assigneeId, string taskTitle, Guid taskId)
        {
            try
            {
                var user = await _db.Users.FindAsync(assigneeId);
                if (user == null || string.IsNullOrWhiteSpace(user.Email) || !user.NotificationsEnabled) return;

                var subject = $"You have been assigned a task: \"{taskTitle}\"";
                var body = $"""
                    <p>Hi,</p>
                    <p>You have been assigned the following task in <strong>DenoLite</strong>:</p>
                    <p><strong>{System.Net.WebUtility.HtmlEncode(taskTitle)}</strong></p>
                    <p><a href="/Tasks/Details/{taskId}?tab=overview">View task</a></p>
                    <p>— The DenoLite Team</p>
                    """;

                await _emailSender.SendAsync(user.Email, subject, body);
            }
            catch
            {
                // email failure must never break add-member flow
            }
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

            // Get active members
            var activeMembers = await (
                from pm in _db.ProjectMembers
                join u in _db.Users on pm.UserId equals u.Id
                where pm.ProjectId == projectId
                orderby (pm.Role == "Owner") descending, pm.UserId
                select new ProjectMemberDto
                {
                    UserId = pm.UserId,
                    Role = pm.Role,
                    Email = u.Email,
                    IsRemoved = false
                }
            ).ToListAsync();

            // Get removed members from activity logs (only for owners)
            var isOwner = await _db.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId && pm.Role == "Owner");

            var removedMembers = new List<ProjectMemberDto>();
            if (isOwner)
            {
                // Find activity logs for member removals
                var removalLogs = await _db.ActivityLogs
                    .Where(a => a.ProjectId == projectId && 
                                (a.ActionType == "MemberRemoved" || a.ActionType == "MemberLeft"))
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();

                // Extract emails from removal logs and look up user IDs
                var removedEmails = removalLogs
                    .SelectMany(log => ExtractEmailFromMessage(log.Message))
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Distinct()
                    .ToList();

                if (removedEmails.Any())
                {
                    // Get user IDs for removed emails (excluding current active members)
                    var activeUserIds = activeMembers.Select(m => m.UserId).ToHashSet();
                    
                    var removedUsers = await _db.Users
                        .Where(u => removedEmails.Contains(u.Email.ToLower()))
                        .Select(u => new { u.Id, u.Email })
                        .ToListAsync();

                    var removedUsersFiltered = removedUsers
                        .Where(u => !activeUserIds.Contains(u.Id))
                        .ToList();

                    removedMembers = removedUsersFiltered.Select(u => new ProjectMemberDto
                    {
                        UserId = u.Id,
                        Email = u.Email,
                        Role = "Member", // Default role for removed members
                        IsRemoved = true
                    }).ToList();
                }
            }

            return activeMembers.Concat(removedMembers).ToList();
        }

        private List<string> ExtractEmailFromMessage(string message)
        {
            var emails = new List<string>();
            if (string.IsNullOrWhiteSpace(message))
                return emails;

            // Extract email from messages like "Member added: email@example.com" or "Member removed: email@example.com"
            var emailPattern = @"(?:Member\s+(?:added|removed|left):\s*)?([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})";
            var matches = System.Text.RegularExpressions.Regex.Matches(message, emailPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    emails.Add(match.Groups[1].Value.ToLower());
                }
            }
            return emails;
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

            // Get member info before removal for activity log
            var memberToRemove = await _db.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == memberUserId);

            if (memberToRemove == null)
                throw new KeyNotFoundException("Member not found");

            var memberEmail = await _db.Users
                .Where(u => u.Id == memberUserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            // If current user is NOT owner -> can only remove himself (leave project)
            if (!string.Equals(currentMember.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                if (memberUserId != currentUserId)
                    throw new ForbiddenException("Members can only remove themselves (leave the project).");

                // Member leaving: remove their membership record
                _db.ProjectMembers.Remove(memberToRemove);
                await _db.SaveChangesAsync();

                // Log activity
                await _activity.LogAsync(
                    projectId: projectId,
                    taskId: null,
                    actorId: currentUserId,
                    actionType: "MemberLeft",
                    message: $"Member left: {memberEmail ?? "Unknown"}"
                );
                return;
            }

            // Owner flow: can remove other members, but cannot remove the project owner
            if (memberUserId == project.OwnerId)
                throw new ConflictException("You cannot remove the project owner.");

            _db.ProjectMembers.Remove(memberToRemove);
            await _db.SaveChangesAsync();

            // Log activity
            await _activity.LogAsync(
                projectId: projectId,
                taskId: null,
                actorId: currentUserId,
                actionType: "MemberRemoved",
                message: $"Member removed: {memberEmail ?? "Unknown"}"
            );
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
