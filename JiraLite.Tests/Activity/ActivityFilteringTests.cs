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

public class ActivityFilteringTests : TestBase
{
    public ActivityFilteringTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Can_Filter_Project_Activity_By_ActionType()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // create task -> TaskCreated log
        var taskId = await CreateTaskViaApiAsync(project.Id, member.Id, "Filter test");

        // update task -> TaskUpdated log
        var updateDto = new TaskItemDto
        {
            Title = "Filter test - changed",
            Description = "Updated",
            Status = JiraTaskStatus.InProgress,
            Priority = 2,
            ProjectId = project.Id,
            AssigneeId = member.Id,
            DueDate = DateTime.UtcNow.AddDays(5)
        };

        var updateResp = await Client.PutAsJsonAsync($"/api/tasks/{taskId}", updateDto);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var resp = await Client.GetAsync($"/api/activity/project/{project.Id}?actionType=TaskUpdated&page=1&pageSize=50");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await resp.Content.ReadFromJsonAsync<PagedResult<ActivityLogDto>>();
        page.Should().NotBeNull();

        // Assert
        page!.Items.Should().NotBeEmpty();
        page.Items.Should().OnlyContain(x => x.ActionType == "TaskUpdated");
    }
    [Fact]
    public async Task Can_Filter_Project_Activity_By_TaskId()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var task1 = await CreateTaskViaApiAsync(project.Id, member.Id, "Task 1");
        var task2 = await CreateTaskViaApiAsync(project.Id, member.Id, "Task 2");

        // Act
        var resp = await Client.GetAsync(
            $"/api/activity/project/{project.Id}?taskId={task1}&page=1&pageSize=50");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await resp.Content.ReadFromJsonAsync<PagedResult<ActivityLogDto>>();

        // Assert
        page.Should().NotBeNull();
        page!.Items.Should().OnlyContain(x => x.TaskId == task1);
    }


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
}
