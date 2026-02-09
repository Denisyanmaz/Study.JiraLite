using JiraLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
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
    }
}
