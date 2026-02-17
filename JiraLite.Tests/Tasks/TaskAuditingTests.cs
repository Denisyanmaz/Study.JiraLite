using FluentAssertions;
using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Task;
using JiraLite.Domain.Entities;
using JiraLite.Domain.Enums;
using JiraLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace JiraLite.Tests.Tasks
{
    public class TaskAuditingTests : TestBase
    {
        public TaskAuditingTests(CustomWebApplicationFactory factory) : base(factory) { }

        [Fact]
        public async Task Create_Task_Sets_CreatedBy_And_CreatedAt_And_Leaves_UpdatedAt_Null()
        {
            // Arrange
            var (owner, member, project) = await SeedProjectWithMemberAsync();
            SetAuth(member);

            var before = DateTime.UtcNow;

            var dto = new TaskItemDto
            {
                Title = "Audit create",
                Description = "Check fields",
                Status = JiraTaskStatus.Todo,
                Priority = 2,
                ProjectId = project.Id,
                AssigneeId = member.Id,
                DueDate = DateTime.UtcNow.AddDays(1)
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Tasks", dto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var created = await response.Content.ReadFromJsonAsync<TaskItem>();
            created.Should().NotBeNull();

            var dbTask = await Db.Tasks.SingleAsync(t => t.Id == created!.Id);

            dbTask.CreatedBy.Should().Be(member.Id);
            dbTask.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
            dbTask.UpdatedAt.Should().BeNull();
        }

        [Fact]
        public async Task Update_Task_Sets_UpdatedAt_And_Does_Not_Change_CreatedAt_Or_CreatedBy()
        {
            // Arrange
            var (owner, member, project) = await SeedProjectWithMemberAsync();
            SetAuth(member);

            // Create task via API (so CreatedBy is populated through DbContext audit)
            var createDto = new TaskItemDto
            {
                Title = "Audit update - original",
                Description = "Before",
                Status = JiraTaskStatus.Todo,
                Priority = 3,
                ProjectId = project.Id,
                AssigneeId = member.Id,
                DueDate = DateTime.UtcNow.AddDays(2)
            };

            var createResp = await Client.PostAsJsonAsync("/api/Tasks", createDto);
            createResp.StatusCode.Should().Be(HttpStatusCode.Created);

            var created = await createResp.Content.ReadFromJsonAsync<TaskItem>();
            created.Should().NotBeNull();

            var beforeEntity = await Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == created!.Id);
            beforeEntity.CreatedBy.Should().Be(member.Id);
            beforeEntity.UpdatedAt.Should().BeNull();

            // Ensure the update definitely causes a modification
            var updateDto = new TaskItemDto
            {
                Title = "Audit update - changed",
                Description = "After",
                Status = JiraTaskStatus.InProgress,
                Priority = 1,
                ProjectId = project.Id,
                AssigneeId = member.Id,
                DueDate = DateTime.UtcNow.AddDays(5)
            };

            var beforeUpdateTime = DateTime.UtcNow;

            // Act
            var updateResp = await Client.PutAsJsonAsync($"/api/Tasks/{created.Id}", updateDto);

            // Assert
            updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

            var afterEntity = await Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == created.Id);

            afterEntity.CreatedAt.Should().Be(beforeEntity.CreatedAt);
            afterEntity.CreatedBy.Should().Be(beforeEntity.CreatedBy);

            afterEntity.UpdatedAt.Should().NotBeNull();
            afterEntity.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));

            afterEntity.Title.Should().Be("Audit update - changed");
            afterEntity.Status.Should().Be(JiraTaskStatus.InProgress);
        }

        // -----------------------
        // helpers
        // -----------------------
    }
}
