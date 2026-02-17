using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Task;
using JiraLite.Domain.Entities;
using JiraLite.Domain.Enums;
using JiraLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;

namespace JiraLite.Tests.Tasks
{
    public class TasksTests : TestBase
    {
        public TasksTests(CustomWebApplicationFactory factory) : base(factory) { }

        private async Task<(User owner, User memberAssignee, User memberOther, User outsider, Project project)> SeedProjectWithMembersAsync()
        {
            var owner = TestHelpers.CreateUser("owner@test.com");
            var memberAssignee = TestHelpers.CreateUser("assignee@test.com");
            var memberOther = TestHelpers.CreateUser("othermember@test.com");
            var outsider = TestHelpers.CreateUser("outsider@test.com");

            Db.Users.AddRange(owner, memberAssignee, memberOther, outsider);

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "TaskAuth Project",
                OwnerId = owner.Id
            };

            Db.Projects.Add(project);

            Db.ProjectMembers.AddRange(
                new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = "Owner" },
                new ProjectMember { ProjectId = project.Id, UserId = memberAssignee.Id, Role = "Member" },
                new ProjectMember { ProjectId = project.Id, UserId = memberOther.Id, Role = "Member" }
            );

            await Db.SaveChangesAsync();

            return (owner, memberAssignee, memberOther, outsider, project);
        }

        private async Task<TaskItem> CreateTaskAsAsync(User creator, Guid projectId, Guid assigneeId)
        {
            // 🔐 Ensure creator IS a project member (required by API)
            if (!await Db.ProjectMembers.AnyAsync(pm =>
                    pm.ProjectId == projectId && pm.UserId == creator.Id))
            {
                Db.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = creator.Id,
                    Role = "Member"
                });

                await Db.SaveChangesAsync();
            }

            SetAuth(creator);

            var dto = new TaskItemDto
            {
                Title = "Test Task",
                Description = "Task for auth tests",
                Status = JiraTaskStatus.Todo,
                Priority = 3,
                ProjectId = projectId,
                AssigneeId = assigneeId,
                DueDate = DateTime.UtcNow.AddDays(3)
            };

            var response = await Client.PostAsJsonAsync("/api/Tasks", dto);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var created = await response.Content.ReadFromJsonAsync<TaskItem>();
            created.Should().NotBeNull();
            created!.Id.Should().NotBe(Guid.Empty);

            return created;
        }


        // ✅ 1) Member can create task
        [Fact]
        public async Task Member_Can_Create_Task()
        {
            var (_, memberAssignee, _, _, project) = await SeedProjectWithMembersAsync();

            SetAuth(memberAssignee);

            var dto = new TaskItemDto
            {
                Title = "Member creates",
                Description = "Should succeed",
                Status = JiraTaskStatus.Todo,
                Priority = 2,
                ProjectId = project.Id,
                AssigneeId = memberAssignee.Id,
                DueDate = DateTime.UtcNow.AddDays(1)
            };

            var response = await Client.PostAsJsonAsync("/api/Tasks", dto);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // ✅ 2) Non-member cannot create task
        [Fact]
        public async Task NonMember_Cannot_Create_Task()
        {
            var (_, memberAssignee, _, outsider, project) = await SeedProjectWithMembersAsync();

            SetAuth(outsider);

            var dto = new TaskItemDto
            {
                Title = "Outsider creates",
                Description = "Should be forbidden",
                Status = JiraTaskStatus.Todo,
                Priority = 3,
                ProjectId = project.Id,
                AssigneeId = memberAssignee.Id,
                DueDate = DateTime.UtcNow.AddDays(2)
            };

            var response = await Client.PostAsJsonAsync("/api/Tasks", dto);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // ✅ 3) Assignee can update
        [Fact]
        public async Task Assignee_Can_Update_Task()
        {
            var (owner, memberAssignee, _, _, project) = await SeedProjectWithMembersAsync();

            var task = await CreateTaskAsAsync(owner, project.Id, memberAssignee.Id);

            SetAuth(memberAssignee);

            var update = new TaskItemDto
            {
                Title = "Updated by assignee",
                Description = "Allowed",
                Status = JiraTaskStatus.InProgress,
                Priority = 1,
                ProjectId = project.Id,
                AssigneeId = memberAssignee.Id,
                DueDate = DateTime.UtcNow.AddDays(5)
            };

            var response = await Client.PutAsJsonAsync($"/api/Tasks/{task.Id}", update);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // ✅ 4) Owner can update
        [Fact]
        public async Task Owner_Can_Update_Task()
        {
            var (owner, memberAssignee, _, _, project) = await SeedProjectWithMembersAsync();

            var task = await CreateTaskAsAsync(owner, project.Id, memberAssignee.Id);

            SetAuth(owner);

            var update = new TaskItemDto
            {
                Title = "Updated by owner",
                Description = "Allowed",
                Status = JiraTaskStatus.Done,
                Priority = 5,
                ProjectId = project.Id,
                AssigneeId = memberAssignee.Id,
                DueDate = DateTime.UtcNow.AddDays(10)
            };

            var response = await Client.PutAsJsonAsync($"/api/Tasks/{task.Id}", update);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // ✅ 5) Other member cannot update
        [Fact]
        public async Task OtherMember_Cannot_Update_Task()
        {
            var (owner, memberAssignee, memberOther, _, project) = await SeedProjectWithMembersAsync();

            var task = await CreateTaskAsAsync(owner, project.Id, memberAssignee.Id);

            SetAuth(memberOther);

            var update = new TaskItemDto
            {
                Title = "Updated by other member",
                Description = "Should be forbidden",
                Status = JiraTaskStatus.InProgress,
                Priority = 2,
                ProjectId = project.Id,
                AssigneeId = memberAssignee.Id,
                DueDate = DateTime.UtcNow.AddDays(7)
            };

            var response = await Client.PutAsJsonAsync($"/api/Tasks/{task.Id}", update);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // ✅ 6) Assignee can delete
        [Fact]
        public async Task Assignee_Can_Delete_Task()
        {
            var (owner, memberAssignee, _, _, project) = await SeedProjectWithMembersAsync();

            var task = await CreateTaskAsAsync(owner, project.Id, memberAssignee.Id);

            SetAuth(memberAssignee);

            var response = await Client.DeleteAsync($"/api/Tasks/{task.Id}");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // ✅ 7) Owner can delete
        [Fact]
        public async Task Owner_Can_Delete_Task()
        {
            var (owner, memberAssignee, _, _, project) = await SeedProjectWithMembersAsync();

            var task = await CreateTaskAsAsync(owner, project.Id, memberAssignee.Id);

            SetAuth(owner);

            var response = await Client.DeleteAsync($"/api/Tasks/{task.Id}");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // ✅ 8) Other member cannot delete
        [Fact]
        public async Task OtherMember_Cannot_Delete_Task()
        {
            var (owner, memberAssignee, memberOther, _, project) = await SeedProjectWithMembersAsync();

            var task = await CreateTaskAsAsync(owner, project.Id, memberAssignee.Id);

            SetAuth(memberOther);

            var response = await Client.DeleteAsync($"/api/Tasks/{task.Id}");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
