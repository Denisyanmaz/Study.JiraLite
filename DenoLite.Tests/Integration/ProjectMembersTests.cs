using FluentAssertions;
using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.ProjectMember;
using DenoLite.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DenoLite.Tests.Integration
{
    public class ProjectMembersTests : TestBase
    {
        public ProjectMembersTests(CustomWebApplicationFactory factory)
            : base(factory) { }

        [Fact]
        public async Task NonOwner_Cannot_Add_Project_Member()
        {
            // Arrange
            var owner = TestHelpers.CreateUser("owner@test.com");
            var nonOwner = TestHelpers.CreateUser("user@test.com");
            var newUser = TestHelpers.CreateUser("new@test.com");

            Db.Users.AddRange(owner, nonOwner, newUser);

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Test Project",
                OwnerId = owner.Id
            };

            Db.Projects.Add(project);

            Db.ProjectMembers.AddRange(
                new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = "Owner" },
                new ProjectMember { ProjectId = project.Id, UserId = nonOwner.Id, Role = "Member" }
            );

            await Db.SaveChangesAsync();

            var token = TestHelpers.GenerateJwt(nonOwner);
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await Client.PostAsJsonAsync(
                $"/api/Projects/{project.Id}/members",
                new ProjectMemberDto { UserId = newUser.Id }
            );

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Owner_Can_Add_Project_Member()
        {
            // Arrange
            var owner = TestHelpers.CreateUser("owner@test.com");
            var newMember = TestHelpers.CreateUser("member@test.com");

            Db.Users.AddRange(owner, newMember);

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Owner Project",
                OwnerId = owner.Id
            };

            Db.Projects.Add(project);

            Db.ProjectMembers.Add(
                new ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = owner.Id,
                    Role = "Owner"
                });

            await Db.SaveChangesAsync();

            var token = TestHelpers.GenerateJwt(owner);
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await Client.PostAsJsonAsync(
                $"/api/Projects/{project.Id}/members",
                new ProjectMemberDto
                {
                    UserId = newMember.Id,
                    Role = "Member"
                });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            // Verify DB state
            var addedMember = await Db.ProjectMembers
                .FirstOrDefaultAsync(pm =>
                    pm.ProjectId == project.Id &&
                    pm.UserId == newMember.Id);

            addedMember.Should().NotBeNull();
            addedMember!.Role.Should().Be("Member");
        }

        [Fact]
        public async Task Duplicate_Member_Cannot_Be_Added()
        {
            // Arrange
            var owner = TestHelpers.CreateUser("owner@test.com");
            var member = TestHelpers.CreateUser("member@test.com");

            Db.Users.AddRange(owner, member);

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Duplicate Test Project",
                OwnerId = owner.Id
            };

            Db.Projects.Add(project);

            // Owner + member already in project
            Db.ProjectMembers.AddRange(
                new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = "Owner" },
                new ProjectMember { ProjectId = project.Id, UserId = member.Id, Role = "Member" }
            );

            await Db.SaveChangesAsync();

            var token = TestHelpers.GenerateJwt(owner);
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // Act: try to add same member again
            var response = await Client.PostAsJsonAsync(
                $"/api/Projects/{project.Id}/members",
                new ProjectMemberDto { UserId = member.Id, Role = "Member" }
            );

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Anonymous_Cannot_Add_Project_Member()
        {
            // Arrange
            var owner = TestHelpers.CreateUser("owner@test.com");
            var newUser = TestHelpers.CreateUser("new@test.com");

            Db.Users.AddRange(owner, newUser);

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Auth Test Project",
                OwnerId = owner.Id
            };

            Db.Projects.Add(project);

            Db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = owner.Id,
                Role = "Owner"
            });

            await Db.SaveChangesAsync();

            // IMPORTANT: no Authorization header
            Client.DefaultRequestHeaders.Authorization = null;

            // Act
            var response = await Client.PostAsJsonAsync(
                $"/api/Projects/{project.Id}/members",
                new ProjectMemberDto { UserId = newUser.Id, Role = "Member" }
            );

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
