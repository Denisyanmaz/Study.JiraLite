using FluentAssertions;
using DenoLite.Application.DTOs.Auth;
using DenoLite.Application.DTOs.Project;
using DenoLite.Application.DTOs.Task;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Tests.Integration;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Integration;

public class ValidationErrorHandlingTests : TestBase
{
    public ValidationErrorHandlingTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Register_With_Invalid_Email_Returns_400()
    {
        // Arrange
        var dto = new RegisterUserDto
        {
            Email = "invalid-email", // Invalid email format
            Password = "Password123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_With_Short_Password_Returns_400()
    {
        // Arrange
        var dto = new RegisterUserDto
        {
            Email = "test@test.com",
            Password = "12345" // Too short (less than 6 characters)
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProject_With_Empty_Name_Returns_400()
    {
        // Arrange
        var user = TestHelpers.CreateUser("user@test.com");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var dto = new CreateProjectDto
        {
            Name = string.Empty, // Empty name
            Description = "Description"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/projects", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTask_With_Invalid_Priority_Returns_400()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var dto = new TaskItemDto
        {
            Title = "Test Task",
            Description = "Description",
            Status = DenoTaskStatus.Todo,
            Priority = 10, // Invalid priority (should be 1-5)
            ProjectId = project.Id,
            AssigneeId = member.Id,
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/tasks", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTaskStatus_With_Invalid_Status_Returns_400()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        SetAuth(member);

        // Try to send invalid status value (this would be caught by JSON deserialization)
        var invalidJson = "{\"status\":\"InvalidStatus\"}";

        // Act
        var response = await Client.PatchAsync(
            $"/api/tasks/{task.Id}/status",
            new StringContent(invalidJson, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        // This might return 400 (Bad Request) due to JSON deserialization failure
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }
}
