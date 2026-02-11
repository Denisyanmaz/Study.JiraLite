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

public class ActivitySearchQueryTests : TestBase
{
    public ActivitySearchQueryTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Can_Search_Project_Activity_By_Q()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var taskId = await CreateTaskViaApiAsync(project.Id, member.Id, "Search Q test");

        // unique token so search is deterministic
        var token = "UNIQUE_TOKEN_9F2A1";
        var comment = new CreateCommentDto { Body = $"This is {token} inside a comment." };

        var cResp = await Client.PostAsJsonAsync($"/api/comments/task/{taskId}", comment);
        cResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var resp = await Client.GetAsync($"/api/activity/project/{project.Id}?q={token}&page=1&pageSize=50");

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await resp.Content.ReadFromJsonAsync<PagedResult<ActivityLogDto>>();
        page.Should().NotBeNull();

        page!.Items.Should().NotBeEmpty();
        page.Items.Should().Contain(x => x.ActionType == "CommentAdded");
        page.Items.Should().OnlyContain(x => x.Message.Contains(token));
    }
    [Fact]
    public async Task Searching_Project_Activity_With_Unknown_Q_Returns_Empty_And_TotalCount_Zero()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // Create at least one activity so we know the project has logs
        var taskId = await CreateTaskViaApiAsync(project.Id, member.Id, "Search negative test");

        var knownToken = "KNOWN_TOKEN_ABC123";
        var cResp = await Client.PostAsJsonAsync(
            $"/api/comments/task/{taskId}",
            new CreateCommentDto { Body = $"This contains {knownToken}" });

        cResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var unknown = "DOES_NOT_EXIST_9C18F";
        var resp = await Client.GetAsync($"/api/activity/project/{project.Id}?q={unknown}&page=1&pageSize=50");

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await resp.Content.ReadFromJsonAsync<PagedResult<ActivityLogDto>>();
        page.Should().NotBeNull();

        page!.TotalCount.Should().Be(0);
        page.Items.Should().BeEmpty();
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
