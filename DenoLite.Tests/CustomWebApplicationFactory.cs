using DenoLite.Application.Interfaces;
using DenoLite.Infrastructure.Persistence;
using DenoLite.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace DenoLite.Tests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _dbName = $"DenoLite_test_{Guid.NewGuid():N}";

        private PostgreSqlContainer _pg = default!;

        public CustomWebApplicationFactory()
        {
            // ✅ Set JWT config as environment variables BEFORE host is built (so Program.cs can read them)
            // Use double underscore (__) for nested config keys in environment variables
            Environment.SetEnvironmentVariable("Jwt__Key", "ThisIsASuperSecretKeyForJWT1234567890!");
            Environment.SetEnvironmentVariable("Jwt__Issuer", "DenoLite.Api");
            Environment.SetEnvironmentVariable("Jwt__Audience", "DenoLite.Api");
            
            // ✅ Set Google OAuth config for tests (dummy values - not used for actual Google auth in tests)
            Environment.SetEnvironmentVariable("Google__ClientId", "test-client-id");
            Environment.SetEnvironmentVariable("Google__ClientSecret", "test-client-secret");
            
            // ✅ Set OTP secret for email verification codes
            Environment.SetEnvironmentVariable("Otp__Secret", "test-otp-secret-key-for-email-verification-1234567890");
            
            // ✅ Prevent .env file loading by setting a flag (Program.cs checks ASPNETCORE_ENVIRONMENT)
            // We'll set it to IntegrationTesting which won't match Production, but we'll handle it
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTesting");
        }


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

                // Replace email sender with test implementation (no-op, doesn't send real emails)
                services.RemoveAll<IEmailSender>();
                services.AddScoped<IEmailSender, TestEmailSender>();
                
                // Configure EmailSettings (required even though TestEmailSender doesn't use it)
                // This prevents any issues if SmtpEmailSender is instantiated elsewhere
                services.Configure<DenoLite.Infrastructure.Services.EmailSettings>(options =>
                {
                    options.Host = "localhost";
                    options.Port = 1025;
                    options.FromEmail = "test@denolite.local";
                    options.FromName = "DenoLite Test";
                    options.UseSsl = false;
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
                    // ✅ Google OAuth config for tests (dummy values - not used for actual Google auth)
                    ["Google:ClientId"] = "test-client-id",
                    ["Google:ClientSecret"] = "test-client-secret",
                    // ✅ OTP secret for email verification codes
                    ["Otp:Secret"] = "test-otp-secret-key-for-email-verification-1234567890",
                    // ✅ Email settings (required by Program.cs, even though TestEmailSender doesn't use them)
                    ["Email:Host"] = "localhost",
                    ["Email:Port"] = "1025",
                    ["Email:FromEmail"] = "test@denolite.local",
                    ["Email:FromName"] = "DenoLite Test",
                    ["Email:UseSsl"] = "false",
                });
            });
        }
    }
}
