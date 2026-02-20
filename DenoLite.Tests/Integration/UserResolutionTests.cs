using FluentAssertions;
using DenoLite.Domain.Entities;
using DenoLite.Tests.Integration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DenoLite.Tests.Integration;

public class UserResolutionTests : TestBase
{
    public UserResolutionTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ResolveUser_By_Email_Returns_UserId()
    {
        // Arrange
        var user = TestHelpers.CreateUser("resolve@test.com");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        // Act
        var response = await Client.GetAsync($"/api/users/resolve?q={user.Email}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("userId").GetString().Should().Be(user.Id.ToString());
    }

    [Fact]
    public async Task ResolveUser_By_Guid_Returns_Same_Guid()
    {
        // Arrange
        var user = TestHelpers.CreateUser("resolveguid@test.com");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        // Act
        var response = await Client.GetAsync($"/api/users/resolve?q={user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("userId").GetString().Should().Be(user.Id.ToString());
    }

    [Fact]
    public async Task ResolveUser_Non_Existent_Email_Returns_404()
    {
        // Arrange
        var user = TestHelpers.CreateUser("resolver@test.com");
        user.IsEmailVerified = true;
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        SetAuth(user);

        // Act
        var response = await Client.GetAsync("/api/users/resolve?q=nonexistent@test.com");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResolveUser_Without_Auth_Returns_401()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync("/api/users/resolve?q=test@test.com");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
