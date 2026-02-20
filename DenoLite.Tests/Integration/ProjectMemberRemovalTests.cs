using FluentAssertions;
using DenoLite.Domain.Entities;
using DenoLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using Xunit;

namespace DenoLite.Tests.Integration;

public class ProjectMemberRemovalTests : TestBase
{
    public ProjectMemberRemovalTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Owner_Can_Remove_Member()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(owner);

        // Act
        var response = await Client.DeleteAsync($"/api/projects/{project.Id}/members/{member.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var memberStillExists = await Db.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == member.Id);
        memberStillExists.Should().BeFalse();
    }

    [Fact]
    public async Task Owner_Cannot_Remove_Self()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(owner);

        // Act
        var response = await Client.DeleteAsync($"/api/projects/{project.Id}/members/{owner.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("cannot remove the project owner");
    }

    [Fact]
    public async Task Member_Can_Leave_Project()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // Act
        var response = await Client.DeleteAsync($"/api/projects/{project.Id}/members/{member.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var memberStillExists = await Db.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == member.Id);
        memberStillExists.Should().BeFalse();
    }

    [Fact]
    public async Task Member_Cannot_Remove_Other_Member()
    {
        // Arrange
        var owner = TestHelpers.CreateUser("owner@test.com");
        var member1 = TestHelpers.CreateUser("member1@test.com");
        var member2 = TestHelpers.CreateUser("member2@test.com");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            OwnerId = owner.Id
        };

        Db.Users.AddRange(owner, member1, member2);
        Db.Projects.Add(project);
        Db.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = "Owner" },
            new ProjectMember { ProjectId = project.Id, UserId = member1.Id, Role = "Member" },
            new ProjectMember { ProjectId = project.Id, UserId = member2.Id, Role = "Member" }
        );
        await Db.SaveChangesAsync();

        SetAuth(member1);

        // Act
        var response = await Client.DeleteAsync($"/api/projects/{project.Id}/members/{member2.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("only remove themselves");
    }

    [Fact]
    public async Task RemoveMember_Creates_Activity_Log()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(owner);

        // Act
        var response = await Client.DeleteAsync($"/api/projects/{project.Id}/members/{member.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var activityLog = await Db.ActivityLogs
            .FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.ActionType == "MemberRemoved");
        activityLog.Should().NotBeNull();
        activityLog!.Message.Should().ContainEquivalentOf("Member removed");
        activityLog.Message.Should().ContainEquivalentOf(member.Email);
    }

    [Fact]
    public async Task Member_Leaving_Creates_Activity_Log()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // Act
        var response = await Client.DeleteAsync($"/api/projects/{project.Id}/members/{member.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var activityLog = await Db.ActivityLogs
            .FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.ActionType == "MemberLeft");
        activityLog.Should().NotBeNull();
        activityLog!.Message.Should().ContainEquivalentOf("Member left");
        activityLog.Message.Should().ContainEquivalentOf(member.Email);
    }
}
