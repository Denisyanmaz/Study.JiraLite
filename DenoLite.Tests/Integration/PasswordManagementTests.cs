using FluentAssertions;
using DenoLite.Application.DTOs.Auth;
using DenoLite.Domain.Entities;
using DenoLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Integration;

public class PasswordManagementTests : TestBase
{
    public PasswordManagementTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ChangePassword_Requires_Old_Password_For_Regular_Users()
    {
        // Arrange
        var user = TestHelpers.CreateUser("regular@test.com");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var changePasswordDto = new ChangePasswordDto
        {
            OldPassword = string.Empty, // Missing old password
            NewPassword = "NewPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("incorrect");
    }

    [Fact]
    public async Task ChangePassword_Allows_Empty_Old_Password_For_Google_Users()
    {
        // Arrange
        var user = TestHelpers.CreateUser("googlepass@test.com");
        user.PasswordHash = string.Empty; // Google-only user
        user.GoogleId = "google_123";
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var changePasswordDto = new ChangePasswordDto
        {
            OldPassword = string.Empty, // Empty for Google users
            NewPassword = "NewPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Reload entity from database to get updated values (detach tracked entity first)
        Db.Entry(user).State = EntityState.Detached;
        var updatedUser = await Db.Users.FindAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.PasswordHash.Should().NotBeNullOrEmpty();
        BCrypt.Net.BCrypt.Verify("NewPassword123!", updatedUser.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_Rejects_Same_Password()
    {
        // Arrange
        var user = TestHelpers.CreateUser("samepass@test.com");
        var password = "SamePassword123!";
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var changePasswordDto = new ChangePasswordDto
        {
            OldPassword = password,
            NewPassword = password // Same password
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("different");
    }

    [Fact]
    public async Task ChangePassword_Validates_Password_Length()
    {
        // Arrange
        var user = TestHelpers.CreateUser("shortpass@test.com");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass123!");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var changePasswordDto = new ChangePasswordDto
        {
            OldPassword = "OldPass123!",
            NewPassword = "Short" // Too short (less than 6 characters)
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("6");
    }

    [Fact]
    public async Task ChangePassword_Without_Auth_Returns_401()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;

        var changePasswordDto = new ChangePasswordDto
        {
            OldPassword = "OldPass123!",
            NewPassword = "NewPass123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_With_Wrong_Old_Password_Returns_401()
    {
        // Arrange
        var user = TestHelpers.CreateUser("wrongpass@test.com");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        var changePasswordDto = new ChangePasswordDto
        {
            OldPassword = "WrongPassword123!", // Wrong password
            NewPassword = "NewPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("incorrect");
    }
}
