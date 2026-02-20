using FluentAssertions;
using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.Task;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Tests.Integration;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Tasks;

public class TaskAssigneeEmailTests : TestBase
{
    public TaskAssigneeEmailTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetTasksPaged_Includes_Assignee_Email()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        SetAuth(member);

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(1);
        payload.Items[0].AssigneeEmail.Should().Be(member.Email);
        payload.Items[0].AssigneeId.Should().Be(member.Id);
    }

    [Fact]
    public async Task GetTasksPaged_Shows_Email_For_Departed_Member()
    {
        // Arrange
        var owner = TestHelpers.CreateUser("owner@test.com");
        var departedMember = TestHelpers.CreateUser("departed@test.com");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            OwnerId = owner.Id
        };

        Db.Users.AddRange(owner, departedMember);
        Db.Projects.Add(project);
        // Note: departedMember is NOT a project member anymore
        Db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = "Owner" });
        await Db.SaveChangesAsync();

        // Create task assigned to departed member
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = departedMember.Id, // Assigned to departed member
            Title = "Task for departed member",
            Status = DenoTaskStatus.Todo,
            Priority = 3,
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        Db.Tasks.Add(task);
        await Db.SaveChangesAsync();

        SetAuth(owner);

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(1);
        payload.Items[0].AssigneeEmail.Should().Be(departedMember.Email);
        payload.Items[0].AssigneeId.Should().Be(departedMember.Id);
    }

    [Fact]
    public async Task GetTasksPaged_Shows_Email_For_Current_Member()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        SetAuth(owner);

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(1);
        payload.Items[0].AssigneeEmail.Should().Be(member.Email);
        payload.Items[0].AssigneeId.Should().Be(member.Id);
    }
}
