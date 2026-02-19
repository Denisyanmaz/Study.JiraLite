using DenoLite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace DenoLite.Tests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _dbName = $"DenoLite_test_{Guid.NewGuid():N}";

        private PostgreSqlContainer _pg = default!;

        public async Task InitializeAsync()
        {
            _pg = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase(_dbName)
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _pg.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (_pg != null)
                await _pg.DisposeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // IMPORTANT: do NOT use "Testing" here, because Program.cs uses that to register InMemory
            builder.UseEnvironment("IntegrationTesting");

            builder.ConfigureServices(services =>
            {
                // Remove any existing DbContext registrations from Program.cs
                services.RemoveAll<DbContextOptions<DenoLiteDbContext>>();
                services.RemoveAll<DenoLiteDbContext>();

                // Register PostgreSQL for tests
                services.AddDbContext<DenoLiteDbContext>(options =>
                {
                    options.UseNpgsql(_pg.GetConnectionString());
                });

                // Build provider + ensure schema exists
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DenoLiteDbContext>();

                // If you have migrations, prefer:
                db.Database.Migrate();
                // db.Database.EnsureCreated();
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "ThisIsASuperSecretKeyForJWT1234567890!",
                    ["Jwt:Issuer"] = "DenoLite.Api",
                    ["Jwt:Audience"] = "DenoLite.Api",
                });
            });
        }
    }
}
