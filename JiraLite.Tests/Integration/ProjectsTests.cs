using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using JiraLite.Application.DTOs.Project;
using JiraLite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraLite.Tests.Integration
{
    public class ProjectsTests : TestBase
    {
        public ProjectsTests(CustomWebApplicationFactory factory) : base(factory) { }

        // =========================
        // 1️⃣ Create project without JWT → 401
        // =========================
        [Fact]
        public async Task Create_Project_Without_Jwt_Returns_401()
        {
            // Arrange
            Client.DefaultRequestHeaders.Authorization = null;

            var dto = new CreateProjectDto
            {
                Name = "NoAuth Project",
                Description = "Should fail without JWT"
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Projects", dto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // =========================
        // 2️⃣ Authenticated user can create project
        // =========================
        [Fact]
        public async Task Authenticated_User_Can_Create_Project()
        {

            var user = TestHelpers.CreateUser("creator@test.com");
            Db.Users.Add(user);
            await Db.SaveChangesAsync();

            SetAuth(user);

            var dto = new CreateProjectDto
            {
                Name = "Project A",
                Description = "Created by authenticated user"
            };

            var response = await Client.PostAsJsonAsync("/api/Projects", dto);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
            project.Should().NotBeNull();
            project!.OwnerId.Should().Be(user.Id);
        }

        // =========================
        // 3️⃣ Owner is auto-added as member
        // =========================
        [Fact]
        public async Task Owner_Is_Auto_Added_As_Project_Member()
        {

            var owner = TestHelpers.CreateUser("owner@test.com");
            Db.Users.Add(owner);
            await Db.SaveChangesAsync();

            SetAuth(owner);

            var response = await Client.PostAsJsonAsync("/api/Projects",
                new CreateProjectDto { Name = "Owner Membership Project" });

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var project = await response.Content.ReadFromJsonAsync<ProjectDto>();

            var membership = await Db.ProjectMembers.FirstOrDefaultAsync(pm =>
                pm.ProjectId == project!.Id &&
                pm.UserId == owner.Id);

            membership.Should().NotBeNull();
            membership!.Role.Should().Be("Owner");
        }

        // =========================
        // 4️⃣ User sees own projects
        // =========================
        [Fact]
        public async Task User_Sees_Own_Projects()
        {

            var user = TestHelpers.CreateUser("user@test.com");
            Db.Users.Add(user);
            await Db.SaveChangesAsync();

            SetAuth(user);

            await Client.PostAsJsonAsync("/api/Projects", new CreateProjectDto { Name = "P100" });
            await Client.PostAsJsonAsync("/api/Projects", new CreateProjectDto { Name = "P101" });

            var response = await Client.GetAsync("/api/Projects");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var projects = await response.Content.ReadFromJsonAsync<List<ProjectDto>>();
            projects.Should().NotBeNull();
            projects!.Should().HaveCount(2);
        }

        // =========================
        // 5️⃣ Non-member does NOT see project
        // =========================
        [Fact]
        public async Task NonMember_Does_Not_See_Project()
        {

            var owner = TestHelpers.CreateUser("owner@test.com");
            var other = TestHelpers.CreateUser("other@test.com");
            Db.Users.AddRange(owner, other);
            await Db.SaveChangesAsync();

            SetAuth(owner);

            await Client.PostAsJsonAsync("/api/Projects",
                new CreateProjectDto { Name = "Owner Only Project" });

            SetAuth(other);

            var response = await Client.GetAsync("/api/Projects");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var projects = await response.Content.ReadFromJsonAsync<List<ProjectDto>>();
            projects.Should().NotBeNull();
            projects!.Should().BeEmpty();
        }
    }
}
