using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.Project;
using DenoLite.Domain.Entities;

namespace DenoLite.Tests.Integration
{
    public class AuthSecurityTests : TestBase
    {
        public AuthSecurityTests(CustomWebApplicationFactory factory) : base(factory) { }

        [Fact]
        public async Task Jwt_With_Invalid_Signature_Returns_401()
        {
            // Arrange
            // Seed a user (only needed if your pipeline depends on it; safe either way)
            var user = TestHelpers.CreateUser("sig@test.com");
            Db.Users.Add(user);
            Db.SaveChanges();

            // Generate a valid token, then corrupt it so signature becomes invalid
            var validToken = TestHelpers.GenerateJwt(user);

            // Corrupt token: keep header+payload but break signature part
            // JWT format: header.payload.signature
            var parts = validToken.Split('.');
            parts.Length.Should().Be(3);

            var invalidSignatureToken = $"{parts[0]}.{parts[1]}.THIS_SIGNATURE_IS_INVALID";

            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", invalidSignatureToken);

            var dto = new CreateProjectDto
            {
                Name = "Invalid Signature Project",
                Description = "Should be rejected"
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Projects", dto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
