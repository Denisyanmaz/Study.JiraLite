using FluentAssertions;
using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.ProjectMember;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Activity;

public class EnhancedActivityLoggingTests : TestBase
{
    public EnhancedActivityLoggingTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Comment_Creation_Logs_Activity()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        SetAuth(member);

        var commentDto = new DenoLite.Application.DTOs.Comment.CreateCommentDto
        {
            Body = "Test comment"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/comments/task/{task.Id}", commentDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var activity = await Db.ActivityLogs
            .FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.TaskId == task.Id && a.ActionType == "CommentAdded");
        activity.Should().NotBeNull();
        activity!.ActorId.Should().Be(member.Id);
        activity.Message.Should().ContainEquivalentOf("comment");
    }

    [Fact]
    public async Task Task_Status_Update_Logs_Activity()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        SetAuth(member);

        var updateDto = new DenoLite.Application.DTOs.Task.UpdateTaskStatusDto
        {
            Status = DenoTaskStatus.InProgress
        };

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Query fresh from database (the API's DbContext may have saved the activity log)
        // Use AsNoTracking to ensure we get fresh data from the database
        var activity = await Db.ActivityLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.TaskId == task.Id && a.ActionType == "TaskStatusChanged");
        activity.Should().NotBeNull("Activity log should be created when task status is updated");
        activity!.Message.Should().ContainEquivalentOf("Status");
    }

    [Fact]
    public async Task Project_Member_Added_Logs_Activity()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        var newMember = TestHelpers.CreateUser("newmember@test.com");
        newMember.IsEmailVerified = true;
        Db.Users.Add(newMember);
        await Db.SaveChangesAsync();

        SetAuth(owner);

        var addMemberDto = new AddProjectMemberDto
        {
            UserId = newMember.Id,
            Role = "Member"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/members", addMemberDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var activity = await Db.ActivityLogs
            .FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.ActionType == "MemberAdded");
        activity.Should().NotBeNull();
        activity!.Message.Should().ContainEquivalentOf("Member added");
        activity.Message.Should().ContainEquivalentOf(newMember.Email);
    }

    [Fact]
    public async Task Activity_Log_Includes_Actor_Email()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        SetAuth(member);

        var commentDto = new DenoLite.Application.DTOs.Comment.CreateCommentDto
        {
            Body = "Test comment"
        };

        // Act
        await Client.PostAsJsonAsync($"/api/comments/task/{task.Id}", commentDto);

        // Assert
        var activity = await Db.ActivityLogs
            .FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.ActionType == "CommentAdded");
        activity.Should().NotBeNull();
        activity!.ActorId.Should().Be(member.Id);

        // Verify actor email by querying User separately
        var actor = await Db.Users.FindAsync(activity.ActorId);
        actor.Should().NotBeNull();
        actor!.Email.Should().Be(member.Email);
    }

    [Fact]
    public async Task Activity_Filter_By_ActionType_Works()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        SetAuth(member);

        // Create multiple activities
        await Db.ActivityLogs.AddRangeAsync(
            new ActivityLog { ProjectId = project.Id, TaskId = task.Id, ActorId = member.Id, ActionType = "TaskCreated", Message = "Task created" },
            new ActivityLog { ProjectId = project.Id, TaskId = task.Id, ActorId = member.Id, ActionType = "CommentAdded", Message = "Comment added" },
            new ActivityLog { ProjectId = project.Id, TaskId = task.Id, ActorId = member.Id, ActionType = "TaskUpdated", Message = "Task updated" }
        );
        await Db.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/activity/project/{project.Id}?actionType=CommentAdded");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var activity = await response.Content.ReadFromJsonAsync<PagedResult<DenoLite.Application.DTOs.ActivityLogDto>>();
        activity.Should().NotBeNull();
        activity!.Items.Should().OnlyContain(a => a.ActionType == "CommentAdded");
    }
}
