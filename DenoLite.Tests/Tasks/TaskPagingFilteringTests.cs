using FluentAssertions;
using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.Task;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Tests.Integration;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Tasks;

public class TaskPagingFilteringTests : TestBase
{
    public TaskPagingFilteringTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Member_Can_Page_Tasks()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();

        SetAuth(member);

        await SeedTasksAsync(project.Id, assigneeId: member.Id, count: 40);

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?page=2&pageSize=15");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();
        payload!.Page.Should().Be(2);
        payload.PageSize.Should().Be(15);
        payload.TotalCount.Should().Be(40);
        payload.Items.Should().HaveCount(15);
    }
    [Fact]
    public async Task Member_Can_Filter_And_Page_Tasks_Together()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // Seed: 12 Todo, 5 InProgress (total 17)
        var todos = Enumerable.Range(0, 12).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = member.Id,
            Title = $"Todo {i:D2}",
            Description = "Seed",
            Status = DenoTaskStatus.Todo,
            Priority = 3,
            DueDate = DateTime.UtcNow.AddDays(2)
        });

        var inProgress = Enumerable.Range(0, 5).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = member.Id,
            Title = $"InProgress {i:D2}",
            Description = "Seed",
            Status = DenoTaskStatus.InProgress,
            Priority = 3,
            DueDate = DateTime.UtcNow.AddDays(2)
        });

        Db.Tasks.AddRange(todos);
        Db.Tasks.AddRange(inProgress);
        await Db.SaveChangesAsync();

        // Act: page 2 of Todo with pageSize=5 -> should return 5 items (items 6-10 of the 12 todos)
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?status=Todo&page=2&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();

        payload!.TotalCount.Should().Be(12);
        payload.Page.Should().Be(2);
        payload.PageSize.Should().Be(5);
        payload.Items.Should().HaveCount(5);
        payload.Items.Should().OnlyContain(t => t.Status == DenoTaskStatus.Todo);
    }

    [Fact]
    public async Task Member_Can_Filter_By_Status()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();

        SetAuth(member);

        await SeedTasksWithStatusesAsync(project.Id, assigneeId: member.Id);

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?status=Todo&page=1&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();

        payload!.TotalCount.Should().Be(10);
        payload.Items.Should().OnlyContain(t => t.Status == DenoTaskStatus.Todo);
    }

    [Fact]
    public async Task Invalid_PageSize_Returns_400_With_ValidationProblemDetails()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();

        SetAuth(member);

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?page=1&pageSize=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Validation failed");
        body.Status.Should().Be(400);
    }
    [Fact]
    public async Task Member_Can_Filter_By_Priority()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // Seed: 6 tasks with priority=1, 9 tasks with priority=3
        var p1 = Enumerable.Range(0, 6).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = member.Id,
            Title = $"P1 {i:D2}",
            Description = "Seed",
            Status = DenoTaskStatus.Todo,
            Priority = 1,
            DueDate = DateTime.UtcNow.AddDays(3)
        });

        var p3 = Enumerable.Range(0, 9).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = member.Id,
            Title = $"P3 {i:D2}",
            Description = "Seed",
            Status = DenoTaskStatus.Todo,
            Priority = 3,
            DueDate = DateTime.UtcNow.AddDays(3)
        });

        Db.Tasks.AddRange(p1);
        Db.Tasks.AddRange(p3);
        await Db.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?priority=1&page=1&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();

        payload!.TotalCount.Should().Be(6);
        payload.Items.Should().HaveCount(6);
        payload.Items.Should().OnlyContain(t => t.Priority == 1);
    }

    [Fact]
    public async Task NonMember_Cannot_View_Paged_Tasks()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        await SeedTasksAsync(project.Id, assigneeId: member.Id, count: 5);

        var outsider = TestHelpers.CreateUser("outsider@test.com");
        Db.Users.Add(outsider);
        await Db.SaveChangesAsync();

        SetAuth(outsider);

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------
    // helpers
    // -----------------------

    private async Task SeedTasksAsync(Guid projectId, Guid assigneeId, int count)
    {
        var statuses = new[] { DenoTaskStatus.Todo, DenoTaskStatus.InProgress, DenoTaskStatus.Done };

        var tasks = Enumerable.Range(0, count).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AssigneeId = assigneeId,
            Title = $"Task {i:D3}",
            Description = "Seed",
            Status = statuses[i % statuses.Length],
            Priority = (i % 5) + 1,
            DueDate = DateTime.UtcNow.AddDays((i % 10) + 1)
        });

        Db.Tasks.AddRange(tasks);
        await Db.SaveChangesAsync();
    }

    private async Task SeedTasksWithStatusesAsync(Guid projectId, Guid assigneeId)
    {
        IEnumerable<TaskItem> Make(int n, DenoTaskStatus status) =>
            Enumerable.Range(0, n).Select(i => new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                AssigneeId = assigneeId,
                Title = $"{status} {i}",
                Description = "Seed",
                Status = status,
                Priority = 3,
                DueDate = DateTime.UtcNow.AddDays(2)
            });

        Db.Tasks.AddRange(Make(10, DenoTaskStatus.Todo));
        Db.Tasks.AddRange(Make(7, DenoTaskStatus.InProgress));
        Db.Tasks.AddRange(Make(3, DenoTaskStatus.Done));

        await Db.SaveChangesAsync();
    }
}
