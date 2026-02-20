using FluentAssertions;
using DenoLite.Application.DTOs.Auth;
using DenoLite.Domain.Entities;
using DenoLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Integration;

public class EmailVerificationTests : TestBase
{
    public EmailVerificationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Register_Creates_Unverified_User()
    {
        // Arrange: Use unique email to avoid conflicts
        var uniqueEmail = $"unverified{Guid.NewGuid():N}@test.com";
        var dto = new RegisterUserDto
        {
            Email = uniqueEmail,
            Password = "Password123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await Db.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
        user.Should().NotBeNull();
        user!.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Unverified_User_Cannot_Login()
    {
        // Arrange
        var user = TestHelpers.CreateUser("unverified@test.com");
        user.IsEmailVerified = false;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        var loginDto = new LoginUserDto
        {
            Email = "unverified@test.com",
            Password = "Password123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("not verified");
    }

    [Fact]
    public async Task VerifyEmail_With_Invalid_Code_Returns_400()
    {
        // Arrange: Create user and verification record directly in test DB
        // This avoids registration conflicts and ensures we have control over the verification state
        var uniqueEmail = $"invalidcode{Guid.NewGuid():N}@test.com";
        var user = TestHelpers.CreateUser(uniqueEmail);
        user.IsEmailVerified = false;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        // Remove any existing verification records for this user (in case of test isolation issues)
        var existingVerifications = await Db.EmailVerifications.Where(v => v.UserId == user.Id).ToListAsync();
        Db.EmailVerifications.RemoveRange(existingVerifications);
        await Db.SaveChangesAsync();

        // Ensure no verification records exist
        var countBefore = await Db.EmailVerifications.CountAsync(v => v.UserId == user.Id);
        countBefore.Should().Be(0, "No verification records should exist before creating new one");

        // Create a fresh verification record with a hash that won't match "000000"
        var verification = new EmailVerification
        {
            UserId = user.Id,
            CodeHash = "test_hash_that_wont_match_000000", // Any hash that won't match "000000"
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Attempts = 0,
            LastSentAt = DateTime.UtcNow,
            SendCount = 1,
            IsUsed = false
        };
        Db.EmailVerifications.Add(verification);
        await Db.SaveChangesAsync();

        // Use raw SQL to ensure IsUsed is definitely false, bypassing any EF tracking issues
        await Db.Database.ExecuteSqlRawAsync(
            @"UPDATE ""EmailVerifications"" SET ""IsUsed"" = false WHERE ""UserId"" = {0}",
            user.Id);

        // Verify exactly one record exists and it's in the correct state
        var verificationCount = await Db.EmailVerifications.CountAsync(v => v.UserId == user.Id);
        verificationCount.Should().Be(1, "Exactly one verification record should exist");
        
        // Reload from database to ensure we have fresh data
        Db.Entry(verification).State = EntityState.Detached;
        var freshVerification = await Db.EmailVerifications.SingleOrDefaultAsync(v => v.UserId == user.Id);
        freshVerification.Should().NotBeNull();
        freshVerification!.IsUsed.Should().BeFalse("Verification record should not be marked as used before testing");
        freshVerification.UserId.Should().Be(user.Id);

        // Double-check the database state right before API call using raw SQL
        // Force IsUsed to false one more time to ensure it's definitely not marked as used
        await Db.Database.ExecuteSqlRawAsync(
            @"UPDATE ""EmailVerifications"" SET ""IsUsed"" = false WHERE ""UserId"" = {0}",
            user.Id);
        
        // Verify one final time
        Db.Entry(verification).State = EntityState.Detached;
        var finalCheck = await Db.EmailVerifications.SingleOrDefaultAsync(v => v.UserId == user.Id);
        finalCheck.Should().NotBeNull();
        finalCheck!.IsUsed.Should().BeFalse("Verification record must not be marked as used before API call");

        var verifyDto = new VerifyEmailDto
        {
            Email = uniqueEmail,
            Code = "000000" // Invalid code (won't match the hash in the database)
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/verify-email", verifyDto);

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        
        // If we get Conflict, it means IsUsed was true - this shouldn't happen but let's provide helpful error
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Check the actual state in database after the API call
            Db.Entry(verification).State = EntityState.Detached;
            var checkVerification = await Db.EmailVerifications.SingleOrDefaultAsync(v => v.UserId == user.Id);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
                $"Expected 400 BadRequest but got 409 Conflict. " +
                $"Verification record IsUsed={checkVerification?.IsUsed}, " +
                $"Attempts={checkVerification?.Attempts}, " +
                $"Body={body}");
        }
        
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().ContainEquivalentOf("Invalid verification code");
    }

    [Fact]
    public async Task VerifyEmail_With_Expired_Code_Returns_400()
    {
        // Arrange
        var user = TestHelpers.CreateUser("expired@test.com");
        user.IsEmailVerified = false;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        // Create expired verification
        var verification = new EmailVerification
        {
            UserId = user.Id,
            CodeHash = "expired_hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // Expired
            Attempts = 0,
            LastSentAt = DateTime.UtcNow.AddMinutes(-20),
            SendCount = 1,
            IsUsed = false
        };
        Db.EmailVerifications.Add(verification);
        await Db.SaveChangesAsync();

        var verifyDto = new VerifyEmailDto
        {
            Email = "expired@test.com",
            Code = "123456"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/verify-email", verifyDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("expired");
    }

    [Fact]
    public async Task VerifyEmail_With_Used_Code_Returns_409()
    {
        // Arrange
        var user = TestHelpers.CreateUser("usedcode@test.com");
        user.IsEmailVerified = false;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        var verification = new EmailVerification
        {
            UserId = user.Id,
            CodeHash = "used_hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Attempts = 0,
            LastSentAt = DateTime.UtcNow,
            SendCount = 1,
            IsUsed = true // Already used
        };
        Db.EmailVerifications.Add(verification);
        await Db.SaveChangesAsync();

        var verifyDto = new VerifyEmailDto
        {
            Email = "usedcode@test.com",
            Code = "123456"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/verify-email", verifyDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("already used");
    }

    [Fact]
    public async Task ResendVerification_Creates_New_Code()
    {
        // Arrange: Use unique email to avoid conflicts
        var uniqueEmail = $"resend{Guid.NewGuid():N}@test.com";
        
        // Ensure user doesn't already exist
        var existingUser = await Db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == uniqueEmail.ToLower());
        if (existingUser != null)
        {
            Db.Users.Remove(existingUser);
            await Db.SaveChangesAsync();
        }
        
        var user = TestHelpers.CreateUser(uniqueEmail);
        user.IsEmailVerified = false;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        // Remove any existing verification records for this user (in case of test isolation issues)
        var existingVerifications = await Db.EmailVerifications.Where(v => v.UserId == user.Id).ToListAsync();
        Db.EmailVerifications.RemoveRange(existingVerifications);
        await Db.SaveChangesAsync();

        // Create initial verification with LastSentAt set to more than 60 seconds ago to avoid cooldown
        var oldVerification = new EmailVerification
        {
            UserId = user.Id,
            CodeHash = "old_hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Attempts = 0,
            LastSentAt = DateTime.UtcNow.AddMinutes(-2), // More than 60 seconds ago
            SendCount = 1,
            IsUsed = false
        };
        Db.EmailVerifications.Add(oldVerification);
        await Db.SaveChangesAsync();

        // Verify the record state before API call
        Db.Entry(oldVerification).State = EntityState.Detached;
        var freshVerification = await Db.EmailVerifications.SingleOrDefaultAsync(v => v.UserId == user.Id);
        freshVerification.Should().NotBeNull();
        freshVerification!.IsUsed.Should().BeFalse("Verification record should not be marked as used");
        freshVerification.SendCount.Should().Be(1);
        
        // Ensure LastSentAt is definitely more than 60 seconds ago
        var secondsSinceLastSent = (DateTime.UtcNow - freshVerification.LastSentAt).TotalSeconds;
        secondsSinceLastSent.Should().BeGreaterThan(60, "LastSentAt should be more than 60 seconds ago to avoid cooldown");

        var resendDto = new ResendVerificationDto
        {
            Email = uniqueEmail
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/resend-verification", resendDto);

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        
        if (response.StatusCode != HttpStatusCode.OK)
        {
            // Check database state after failed API call
            Db.Entry(oldVerification).State = EntityState.Detached;
            var checkVerification = await Db.EmailVerifications.SingleOrDefaultAsync(v => v.UserId == user.Id);
            var checkUser = await Db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            
            response.StatusCode.Should().Be(HttpStatusCode.OK, 
                $"Expected 200 OK but got {(int)response.StatusCode}. " +
                $"Body: {body}. " +
                $"User exists: {checkUser != null}, IsEmailVerified: {checkUser?.IsEmailVerified}. " +
                $"Verification exists: {checkVerification != null}, IsUsed: {checkVerification?.IsUsed}, " +
                $"SendCount: {checkVerification?.SendCount}, LastSentAt: {checkVerification?.LastSentAt}");
        }
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Reload verification from database (detach tracked entity first)
        Db.Entry(oldVerification).State = EntityState.Detached;
        
        // Old verification should be removed, new one created
        var oldVerificationStillExists = await Db.EmailVerifications
            .AnyAsync(v => v.UserId == user.Id && v.CodeHash == "old_hash");
        oldVerificationStillExists.Should().BeFalse();

        var newVerification = await Db.EmailVerifications
            .FirstOrDefaultAsync(v => v.UserId == user.Id);
        newVerification.Should().NotBeNull();
        newVerification!.CodeHash.Should().NotBe("old_hash");
        newVerification.SendCount.Should().Be(2);
    }

    [Fact]
    public async Task ResendVerification_Rate_Limited()
    {
        // Arrange
        var user = TestHelpers.CreateUser("ratelimit@test.com");
        user.IsEmailVerified = false;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        // Create recent verification (less than 60 seconds ago)
        var verification = new EmailVerification
        {
            UserId = user.Id,
            CodeHash = "rate_limit_hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Attempts = 0,
            LastSentAt = DateTime.UtcNow.AddSeconds(-30), // Only 30 seconds ago
            SendCount = 1,
            IsUsed = false
        };
        Db.EmailVerifications.Add(verification);
        await Db.SaveChangesAsync();

        var resendDto = new ResendVerificationDto
        {
            Email = "ratelimit@test.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/resend-verification", resendDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("wait");
    }
}
