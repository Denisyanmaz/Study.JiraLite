using FluentAssertions;
using DenoLite.Application.DTOs.Auth;
using DenoLite.Domain.Entities;
using DenoLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Integration;

public class EmailChangeTests : TestBase
{
    public EmailChangeTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task RequestEmailChange_Requires_Password()
    {
        // Arrange
        var user = TestHelpers.CreateUser("changemail@test.com");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var requestDto = new RequestEmailChangeDto
        {
            Password = "WrongPassword123!", // Wrong password
            NewEmail = "newemail@test.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/request-email-change", requestDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("incorrect");
    }

    [Fact]
    public async Task RequestEmailChange_Rejects_Duplicate_Email()
    {
        // Arrange
        var user1 = TestHelpers.CreateUser("user1@test.com");
        user1.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        user1.IsEmailVerified = true;
        Db.Users.Add(user1);

        var user2 = TestHelpers.CreateUser("user2@test.com");
        user2.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        user2.IsEmailVerified = true;
        Db.Users.Add(user2);
        await Db.SaveChangesAsync();

        SetAuth(user1);

        var requestDto = new RequestEmailChangeDto
        {
            Password = "Password123!",
            NewEmail = "user2@test.com" // Already exists
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/request-email-change", requestDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("already in use");
    }

    [Fact]
    public async Task RequestEmailChange_Creates_Change_Request()
    {
        // Arrange: Use unique emails to avoid conflicts
        var uniqueEmail = $"requestchange{Guid.NewGuid():N}@test.com";
        var newEmail = $"newemail{Guid.NewGuid():N}@test.com";
        
        var user = TestHelpers.CreateUser(uniqueEmail);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var requestDto = new RequestEmailChangeDto
        {
            Password = "Password123!",
            NewEmail = newEmail
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/request-email-change", requestDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var changeRequest = await Db.EmailChangeRequests
            .FirstOrDefaultAsync(r => r.UserId == user.Id && r.NewEmail.ToLower() == newEmail.ToLower());
        changeRequest.Should().NotBeNull();
        changeRequest!.NewEmail.Should().Be(newEmail);
        changeRequest.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task VerifyEmailChange_With_Invalid_Code_Returns_400()
    {
        // Arrange
        var uniqueEmail = $"verifychange{Guid.NewGuid():N}@test.com";
        var user = TestHelpers.CreateUser(uniqueEmail);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var newEmail = $"newemail{Guid.NewGuid():N}@test.com";

        // Request email change first and verify it succeeds
        var requestResponse = await Client.PostAsJsonAsync("/api/auth/request-email-change", new RequestEmailChangeDto
        {
            Password = "Password123!",
            NewEmail = newEmail
        });
        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Email change request should succeed");

        // Verify EmailChangeRequest was created
        var changeRequest = await Db.EmailChangeRequests.FirstOrDefaultAsync(r => r.UserId == user.Id && r.NewEmail.ToLower() == newEmail.ToLower());
        changeRequest.Should().NotBeNull("EmailChangeRequest should exist after request");

        // Update the CodeHash to something that won't match "000000"
        // This ensures we're testing invalid code scenario, not missing request scenario
        changeRequest!.CodeHash = "invalid_hash_that_wont_match_000000";
        changeRequest.ExpiresAt = DateTime.UtcNow.AddMinutes(15);
        await Db.SaveChangesAsync();

        var verifyDto = new VerifyEmailChangeDto
        {
            NewEmail = newEmail,
            Code = "000000" // Invalid code (won't match the hash)
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/verify-email-change", verifyDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("Invalid verification code");
    }

    [Fact]
    public async Task VerifyEmailChange_With_Expired_Code_Returns_400()
    {
        // Arrange
        var user = TestHelpers.CreateUser("expiredchange@test.com");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        // Create expired change request
        var changeRequest = new EmailChangeRequest
        {
            UserId = user.Id,
            NewEmail = "expirednew@test.com",
            CodeHash = "expired_hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1) // Expired
        };
        Db.EmailChangeRequests.Add(changeRequest);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var verifyDto = new VerifyEmailChangeDto
        {
            NewEmail = "expirednew@test.com",
            Code = "123456"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/verify-email-change", verifyDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("expired");
    }

    [Fact]
    public async Task VerifyEmailChange_Without_Auth_Returns_401()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;

        var verifyDto = new VerifyEmailChangeDto
        {
            NewEmail = "newemail@test.com",
            Code = "123456"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/verify-email-change", verifyDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
