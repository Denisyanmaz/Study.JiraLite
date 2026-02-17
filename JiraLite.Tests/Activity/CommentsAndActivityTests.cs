using FluentAssertions;
using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Comment;
using JiraLite.Application.DTOs.Common;
using JiraLite.Application.DTOs.Task;
using JiraLite.Domain.Entities;
using JiraLite.Domain.Enums;
using JiraLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace JiraLite.Tests.Activity;

public class CommentsAndActivityTests : TestBase
{
    public CommentsAndActivityTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Member_Can_Add_Comment_To_Task()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var taskId = await CreateTaskViaApiAsync(project.Id, assigneeId: member.Id);

        var dto = new CreateCommentDto { Body = "First comment" };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/comments/task/{taskId}", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<CommentDto>();
        created.Should().NotBeNull();
        created!.TaskId.Should().Be(taskId);
        created.AuthorId.Should().Be(member.Id);
        created.Body.Should().Be("First comment");

        // and it's in DB
        var dbComment = await Db.Comments.SingleAsync(c => c.Id == created.Id);
        dbComment.AuthorId.Should().Be(member.Id);
        dbComment.Body.Should().Be("First comment");
    }

    [Fact]
    public async Task NonMember_Cannot_Add_Comment_To_Task()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();

        // Create outsider
        var outsider = TestHelpers.CreateUser("outsider@test.com");
        Db.Users.Add(outsider);
        await Db.SaveChangesAsync();

        // Create task as member
        SetAuth(member);
        var taskId = await CreateTaskViaApiAsync(project.Id, assigneeId: member.Id);

        // Act as outsider
        SetAuth(outsider);
        var response = await Client.PostAsJsonAsync($"/api/comments/task/{taskId}", new CreateCommentDto { Body = "Nope" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Member_Can_List_Task_Comments()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var taskId = await CreateTaskViaApiAsync(project.Id, assigneeId: member.Id);

        await Client.PostAsJsonAsync($"/api/comments/task/{taskId}", new CreateCommentDto { Body = "C1" });
        await Client.PostAsJsonAsync($"/api/comments/task/{taskId}", new CreateCommentDto { Body = "C2" });

        // Act
        var response = await Client.GetAsync($"/api/comments/task/{taskId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<List<CommentDto>>();
        list.Should().NotBeNull();
        list!.Select(x => x.Body).Should().ContainInOrder("C1", "C2");
    }

    [Fact]
    public async Task Cannot_Comment_On_Deleted_Task_Returns_404()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var taskId = await CreateTaskViaApiAsync(project.Id, assigneeId: member.Id);

        // Delete task (soft delete)
        var del = await Client.DeleteAsync($"/api/tasks/{taskId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/comments/task/{taskId}", new CreateCommentDto { Body = "Should fail" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Adding_Comment_Creates_Activity_Log_Entry()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var taskId = await CreateTaskViaApiAsync(project.Id, assigneeId: member.Id);

        // Act
        var resp = await Client.PostAsJsonAsync($"/api/comments/task/{taskId}", new CreateCommentDto { Body = "Log this" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert (query activity via API)
        var activityResp = await Client.GetAsync($"/api/activity/project/{project.Id}?page=1&pageSize=50");
        activityResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await activityResp.Content.ReadFromJsonAsync<PagedResult<ActivityLogDto>>();
        page.Should().NotBeNull();

        page!.Items.Should().Contain(a =>
            a.ActionType == "CommentAdded" &&
            a.ProjectId == project.Id &&
            a.TaskId == taskId &&
            a.ActorId == member.Id
        );
    }

    [Fact]
    public async Task NonMember_Cannot_View_Project_Activity()
    {
        // Arrange
        var (_, member, project) = await SeedProjectWithMemberAsync();

        var outsider = TestHelpers.CreateUser("outsider@test.com");
        Db.Users.Add(outsider);
        await Db.SaveChangesAsync();

        // create a task and comment as member so activity exists
        SetAuth(member);
        var taskId = await CreateTaskViaApiAsync(project.Id, assigneeId: member.Id);
        await Client.PostAsJsonAsync($"/api/comments/task/{taskId}", new CreateCommentDto { Body = "hello" });

        // Act as outsider
        SetAuth(outsider);
        var activityResp = await Client.GetAsync($"/api/activity/project/{project.Id}?page=1&pageSize=50");

        // Assert
        activityResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------
    // local helper (API create task)
    // -----------------------
    private async Task<Guid> CreateTaskViaApiAsync(Guid projectId, Guid assigneeId)
    {
        var dto = new TaskItemDto
        {
            Title = "Task for comments",
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
