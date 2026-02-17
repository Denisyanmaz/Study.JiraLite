using FluentAssertions;
using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Common;
using JiraLite.Application.DTOs.Task;
using JiraLite.Domain.Entities;
using JiraLite.Domain.Enums;
using JiraLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace JiraLite.Tests.Tasks;

public class TaskSoftDeleteTests : TestBase
{
    public TaskSoftDeleteTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Deleted_Task_Does_Not_Appear_In_Paged_List()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // Create via API (so it respects auth rules)
        var dto = new TaskItemDto
        {
            Title = "To be deleted",
            Description = "Seed",
            Status = JiraTaskStatus.Todo,
            Priority = 3,
            ProjectId = project.Id,
            AssigneeId = member.Id,
            DueDate = DateTime.UtcNow.AddDays(2)
        };

        var createResp = await Client.PostAsJsonAsync("/api/Tasks", dto);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResp.Content.ReadFromJsonAsync<TaskItem>();
        created.Should().NotBeNull();

        // Act: delete
        var delResp = await Client.DeleteAsync($"/api/Tasks/{created!.Id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: paged list is empty
        var listResp = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?page=1&pageSize=50");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await listResp.Content.ReadFromJsonAsync<PagedResult<TaskItem>>();
        payload.Should().NotBeNull();

        payload!.Items.Should().NotContain(t => t.Id == created.Id);
        payload.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetById_After_Delete_Returns_404()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var dto = new TaskItemDto
        {
            Title = "Delete then get",
            Description = "Seed",
            Status = JiraTaskStatus.Todo,
            Priority = 3,
            ProjectId = project.Id,
            AssigneeId = member.Id,
            DueDate = DateTime.UtcNow.AddDays(2)
        };

        var createResp = await Client.PostAsJsonAsync("/api/Tasks", dto);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResp.Content.ReadFromJsonAsync<TaskItem>();
        created.Should().NotBeNull();

        // Act
        var delResp = await Client.DeleteAsync($"/api/Tasks/{created!.Id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResp = await Client.GetAsync($"/api/Tasks/{created.Id}");

        // Assert
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_After_Delete_Returns_404()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var dto = new TaskItemDto
        {
            Title = "Delete then update",
            Description = "Seed",
            Status = JiraTaskStatus.Todo,
            Priority = 3,
            ProjectId = project.Id,
            AssigneeId = member.Id,
            DueDate = DateTime.UtcNow.AddDays(2)
        };

        var createResp = await Client.PostAsJsonAsync("/api/Tasks", dto);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResp.Content.ReadFromJsonAsync<TaskItem>();
        created.Should().NotBeNull();

        var delResp = await Client.DeleteAsync($"/api/Tasks/{created!.Id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updateDto = new TaskItemDto
        {
            Title = "Should fail",
            Description = "Deleted",
            Status = JiraTaskStatus.InProgress,
            Priority = 2,
            ProjectId = project.Id,
            AssigneeId = member.Id,
            DueDate = DateTime.UtcNow.AddDays(5)
        };

        // Act
        var putResp = await Client.PutAsJsonAsync($"/api/Tasks/{created.Id}", updateDto);

        // Assert
        putResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Sets_SoftDelete_Fields()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var dto = new TaskItemDto
        {
            Title = "Check deleted fields",
            Description = "Seed",
            Status = JiraTaskStatus.Todo,
            Priority = 3,
            ProjectId = project.Id,
            AssigneeId = member.Id,
            DueDate = DateTime.UtcNow.AddDays(2)
        };

        var createResp = await Client.PostAsJsonAsync("/api/Tasks", dto);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResp.Content.ReadFromJsonAsync<TaskItem>();
        created.Should().NotBeNull();

        // Act
        var delResp = await Client.DeleteAsync($"/api/Tasks/{created!.Id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: query filter hides it, so ignore filters
        var deleted = await Db.Tasks.IgnoreQueryFilters().SingleAsync(t => t.Id == created.Id);

        deleted.IsDeleted.Should().BeTrue();
        deleted.DeletedBy.Should().Be(member.Id);
        deleted.DeletedAt.Should().NotBeNull();
    }

    // ---- helpers ----

}
