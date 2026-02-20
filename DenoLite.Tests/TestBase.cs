using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Headers;
using Xunit;

namespace DenoLite.Tests
{
    public abstract class TestBase : IClassFixture<DenoLite.Tests.Integration.CustomWebApplicationFactory>, IAsyncLifetime
    {
        protected readonly HttpClient Client;
        protected readonly DenoLiteDbContext Db;

        private readonly IServiceScope _scope;

        protected TestBase(DenoLite.Tests.Integration.CustomWebApplicationFactory factory)
        {
            Client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            Db = _scope.ServiceProvider.GetRequiredService<DenoLiteDbContext>();
        }

        public async Task InitializeAsync()
        {
            // Clean DB for each test (fast + deterministic)
            await ResetDbAsync();

            // Prevent auth header leaking between tests
            Client.DefaultRequestHeaders.Authorization = null;
        }

        public Task DisposeAsync()
        {
            _scope.Dispose();
            return Task.CompletedTask;
        }

        private async Task ResetDbAsync()
        {
            // Adjust table names if your EF mappings differ
            await Db.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE ""ActivityLogs"", ""Comments"", ""Tasks"", ""ProjectMembers"", ""Projects"", ""Users"", ""EmailChangeRequests"", ""EmailVerifications""
            RESTART IDENTITY CASCADE;
            ");
        }

        protected void SetAuth(User user)
        {
            Client.DefaultRequestHeaders.Authorization = null;

            var token = TestHelpers.GenerateJwt(user);
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        protected void ClearAuth()
        {
            Client.DefaultRequestHeaders.Authorization = null;
        }
        protected async Task<(User owner, User member, Project project)> SeedProjectWithMemberAsync()
        {
            var owner = TestHelpers.CreateUser("owner@test.com");
            var member = TestHelpers.CreateUser("member@test.com");

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Audit|Paging|Soft-Delete Project",
                Description = "Test",
                OwnerId = owner.Id
            };

            Db.Users.AddRange(owner, member);
            Db.Projects.Add(project);

            Db.ProjectMembers.AddRange(
                new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = "Owner" },
                new ProjectMember { ProjectId = project.Id, UserId = member.Id, Role = "Member" }
            );

            await Db.SaveChangesAsync();
            return (owner, member, project);
        }

        protected async Task<(User owner, User member, Project project, TaskItem task)> CreateProjectWithTaskAsync()
        {
            var (owner, member, project) = await SeedProjectWithMemberAsync();

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "Test Task",
                Description = "Test Description",
                Status = DenoTaskStatus.Todo,
                Priority = 3,
                ProjectId = project.Id,
                AssigneeId = member.Id,
                DueDate = DateTime.UtcNow.AddDays(7)
            };

            Db.Tasks.Add(task);
            await Db.SaveChangesAsync();

            return (owner, member, project, task);
        }
    }
}
