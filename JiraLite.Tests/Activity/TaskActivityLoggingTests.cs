using FluentAssertions;
using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Common;
using JiraLite.Domain.Entities;
using JiraLite.Domain.Enums;
using JiraLite.Tests.Integration;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace JiraLite.Tests.Activity;

public class TaskActivityLoggingTests : TestBase
{
    public TaskActivityLoggingTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Creating_Task_Creates_TaskCreated_Activity()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var createDto = new TaskItemDto
        {
            Title = "Create log test",
            Description = "Seed",
            Status = JiraTaskStatus.Todo,
            Priority = 3,
            ProjectId = project.Id,
            AssigneeId = member.Id,
            DueDate = DateTime.UtcNow.AddDays(2)
        };

        // Act
        var createResp = await Client.PostAsJsonAsync("/api/tasks", createDto);

        // Assert
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResp.Content.ReadFromJsonAsync<TaskItem>();
        created.Should().NotBeNull();

        var activity = await GetProjectActivityAsync(project.Id);

        activity.Items.Should().Contain(a =>
            a.ActionType == "TaskCreated" &&
            a.ProjectId == project.Id &&
            a.TaskId == created!.Id &&
            a.ActorId == member.Id
        );
    }

    [Fact]
    public async Task Updating_Task_Creates_TaskUpdated_Activity()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // Create task first
        var taskId = await CreateTaskViaApiAsync(project.Id, member.Id, "Update log test");

        var updateDto = new TaskItemDto
        {
            Title = "Update log test - changed",
            Description = "Updated",
            Status = JiraTaskStatus.InProgress,
            Priority = 2,
            ProjectId = project.Id,
            AssigneeId = member.Id,
            DueDate = DateTime.UtcNow.AddDays(5)
        };

        // Act
        var updateResp = await Client.PutAsJsonAsync($"/api/tasks/{taskId}", updateDto);

        // Assert
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var activity = await GetProjectActivityAsync(project.Id);

        activity.Items.Should().Contain(a =>
            a.ActionType == "TaskUpdated" &&
            a.ProjectId == project.Id &&
            a.TaskId == taskId &&
            a.ActorId == member.Id
        );
    }

    [Fact]
    public async Task Deleting_Task_Creates_TaskDeleted_Activity()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var taskId = await CreateTaskViaApiAsync(project.Id, member.Id, "Delete log test");

        // Act
        var delResp = await Client.DeleteAsync($"/api/tasks/{taskId}");

        // Assert
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var activity = await GetProjectActivityAsync(project.Id);

        activity.Items.Should().Contain(a =>
            a.ActionType == "TaskDeleted" &&
            a.ProjectId == project.Id &&
            a.TaskId == taskId &&
            a.ActorId == member.Id
        );
    }

    // -----------------------
    // helpers
    // -----------------------

    private async Task<Guid> CreateTaskViaApiAsync(Guid projectId, Guid assigneeId, string title)
    {
        var dto = new TaskItemDto
        {
            Title = title,
            Description = "Seed",
            Status = JiraTaskStatus.Todo,
            Priority = 3,
            ProjectId = projectId,
            AssigneeId = assigneeId,
            DueDate = DateTime.UtcNow.AddDays(2)
        };

        var resp = await Client.PostAsJsonAsync("/api/tasks", dto);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await resp.Content.ReadFromJsonAsync<TaskItem>();
        created.Should().NotBeNull();

        return created!.Id;
    }

    private async Task<PagedResult<ActivityLogDto>> GetProjectActivityAsync(Guid projectId)
    {
        var resp = await Client.GetAsync($"/api/activity/project/{projectId}?page=1&pageSize=50");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await resp.Content.ReadFromJsonAsync<PagedResult<ActivityLogDto>>();
        payload.Should().NotBeNull();

        return payload!;
    }
}
