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

namespace DenoLite.Tests.Activity;

public class EnrichedActivityMessageTests : TestBase
{
    public EnrichedActivityMessageTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task TaskUpdated_Log_Message_Includes_Diff_Details()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var taskId = await CreateTaskViaApiAsync(project.Id, member.Id, "Enrich test");

        var updateDto = new TaskItemDto
        {
            Title = "Enrich test - new title",
            Description = "Updated",
            Status = DenoTaskStatus.InProgress,
            Priority = 1,
            ProjectId = project.Id,
            AssigneeId = member.Id,
            DueDate = DateTime.UtcNow.AddDays(10)
        };

        // Act
        var updateResp = await Client.PutAsJsonAsync($"/api/tasks/{taskId}", updateDto);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var resp = await Client.GetAsync($"/api/activity/task/{taskId}?actionType=TaskUpdated&page=1&pageSize=20");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await resp.Content.ReadFromJsonAsync<PagedResult<ActivityLogDto>>();
        page.Should().NotBeNull();

        // Assert
        var log = page!.Items.FirstOrDefault();
        log.Should().NotBeNull();

        log!.Message.Should().Contain("Task updated");
        log.Message.Should().Contain("Status");
        log.Message.Should().Contain("→");      // shows it contains diffs (arrow character)
        log.Message.Should().Contain("Priority");
    }

    private async Task<Guid> CreateTaskViaApiAsync(Guid projectId, Guid assigneeId, string title)
    {
        var dto = new TaskItemDto
        {
            Title = title,
            Description = "Seed",
            Status = DenoTaskStatus.Todo,
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
