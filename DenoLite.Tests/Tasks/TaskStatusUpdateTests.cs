using FluentAssertions;
using DenoLite.Application.DTOs.Task;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Tasks;

public class TaskStatusUpdateTests : TestBase
{
    public TaskStatusUpdateTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task UpdateTaskStatus_Changes_Status()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        SetAuth(member);

        var updateDto = new UpdateTaskStatusDto
        {
            Status = DenoTaskStatus.InProgress
        };

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify API response contains updated status
        var responseTask = await response.Content.ReadFromJsonAsync<TaskItem>();
        responseTask.Should().NotBeNull();
        responseTask!.Status.Should().Be(DenoTaskStatus.InProgress);

        // Reload entity from database to verify persistence (detach tracked entity first)
        Db.Entry(task).State = EntityState.Detached;
        var updatedTask = await Db.Tasks.FindAsync(task.Id);
        updatedTask.Should().NotBeNull();
        updatedTask!.Status.Should().Be(DenoTaskStatus.InProgress);
    }

    [Fact]
    public async Task UpdateTaskStatus_Without_Auth_Returns_401()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        Client.DefaultRequestHeaders.Authorization = null;

        var updateDto = new UpdateTaskStatusDto
        {
            Status = DenoTaskStatus.Done
        };

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateTaskStatus_Non_Member_Returns_403()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        var nonMember = TestHelpers.CreateUser("nonmember@test.com");
        nonMember.IsEmailVerified = true;
        Db.Users.Add(nonMember);
        await Db.SaveChangesAsync();

        SetAuth(nonMember);

        var updateDto = new UpdateTaskStatusDto
        {
            Status = DenoTaskStatus.Done
        };

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateTaskStatus_Non_Existent_Task_Returns_404()
    {
        // Arrange
        var user = TestHelpers.CreateUser("user@test.com");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var updateDto = new UpdateTaskStatusDto
        {
            Status = DenoTaskStatus.Done
        };

        var nonExistentTaskId = Guid.NewGuid();

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/tasks/{nonExistentTaskId}/status", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
