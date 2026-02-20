using FluentAssertions;
using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.Project;
using DenoLite.Domain.Entities;
using DenoLite.Tests.Integration;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Integration;

public class ProjectPagingTests : TestBase
{
    public ProjectPagingTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetMyProjectsPaged_Returns_Paged_Results()
    {
        // Arrange
        var user = TestHelpers.CreateUser("user@test.com");
        user.IsEmailVerified = true;
        Db.Users.Add(user);

        // Create 25 projects
        var projects = Enumerable.Range(1, 25).Select(i => new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Project {i:D2}",
            Description = $"Description {i}",
            OwnerId = user.Id
        }).ToList();

        Db.Projects.AddRange(projects);
        Db.ProjectMembers.AddRange(projects.Select(p => new ProjectMember
        {
            ProjectId = p.Id,
            UserId = user.Id,
            Role = "Owner"
        }));
        await Db.SaveChangesAsync();

        SetAuth(user);

        // Act
        var response = await Client.GetAsync("/api/projects/paged?page=2&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<ProjectDto>>();
        payload.Should().NotBeNull();
        payload!.Page.Should().Be(2);
        payload.PageSize.Should().Be(10);
        payload.TotalCount.Should().Be(25);
        payload.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetMyProjectsPaged_Returns_Only_User_Projects()
    {
        // Arrange
        var user1 = TestHelpers.CreateUser("user1@test.com");
        user1.IsEmailVerified = true;
        var user2 = TestHelpers.CreateUser("user2@test.com");
        user2.IsEmailVerified = true;

        Db.Users.AddRange(user1, user2);

        // User1 owns 5 projects
        var user1Projects = Enumerable.Range(1, 5).Select(i => new Project
        {
            Id = Guid.NewGuid(),
            Name = $"User1 Project {i}",
            Description = "Description",
            OwnerId = user1.Id
        }).ToList();

        // User2 owns 3 projects
        var user2Projects = Enumerable.Range(1, 3).Select(i => new Project
        {
            Id = Guid.NewGuid(),
            Name = $"User2 Project {i}",
            Description = "Description",
            OwnerId = user2.Id
        }).ToList();

        Db.Projects.AddRange(user1Projects);
        Db.Projects.AddRange(user2Projects);

        Db.ProjectMembers.AddRange(user1Projects.Select(p => new ProjectMember
        {
            ProjectId = p.Id,
            UserId = user1.Id,
            Role = "Owner"
        }));
        Db.ProjectMembers.AddRange(user2Projects.Select(p => new ProjectMember
        {
            ProjectId = p.Id,
            UserId = user2.Id,
            Role = "Owner"
        }));
        await Db.SaveChangesAsync();

        SetAuth(user1);

        // Act
        var response = await Client.GetAsync("/api/projects/paged?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<ProjectDto>>();
        payload.Should().NotBeNull();
        payload!.TotalCount.Should().Be(5);
        payload.Items.Should().HaveCount(5);
        payload.Items.Should().OnlyContain(p => user1Projects.Any(up => up.Id == p.Id));
    }
}
