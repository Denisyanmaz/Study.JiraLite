using JiraLite.Domain.Entities;
using JiraLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Headers;
using Xunit;

namespace JiraLite.Tests
{
    public abstract class TestBase : IClassFixture<JiraLite.Tests.Integration.CustomWebApplicationFactory>, IAsyncLifetime
    {
        protected readonly HttpClient Client;
        protected readonly JiraLiteDbContext Db;

        private readonly IServiceScope _scope;

        protected TestBase(JiraLite.Tests.Integration.CustomWebApplicationFactory factory)
        {
            Client = factory.CreateClient();
            _scope = factory.Services.CreateScope();
            Db = _scope.ServiceProvider.GetRequiredService<JiraLiteDbContext>();
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
                TRUNCATE TABLE ""Tasks"", ""ProjectMembers"", ""Projects"", ""Users""
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
    }
}
