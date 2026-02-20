using FluentAssertions;
using DenoLite.Application.DTOs.Comment;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Comments;

public class CommentAuthorEmailTests : TestBase
{
    public CommentAuthorEmailTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetComments_Includes_Author_Email()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        SetAuth(member);

        // Create a comment
        var commentDto = new CreateCommentDto
        {
            Body = "Test comment"
        };

        await Client.PostAsJsonAsync($"/api/comments/task/{task.Id}", commentDto);

        // Act
        var response = await Client.GetAsync($"/api/comments/task/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var comments = await response.Content.ReadFromJsonAsync<List<CommentDto>>();
        comments.Should().NotBeNull();
        comments!.Should().HaveCount(1);
        comments[0].AuthorEmail.Should().Be(member.Email);
        comments[0].AuthorId.Should().Be(member.Id);
    }

    [Fact]
    public async Task AddComment_Returns_Comment_With_Author_Email()
    {
        // Arrange
        var (owner, member, project, task) = await CreateProjectWithTaskAsync();
        SetAuth(member);

        var commentDto = new CreateCommentDto
        {
            Body = "New comment"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/comments/task/{task.Id}", commentDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var comment = await response.Content.ReadFromJsonAsync<CommentDto>();
        comment.Should().NotBeNull();
        comment!.AuthorEmail.Should().Be(member.Email);
        comment.AuthorId.Should().Be(member.Id);
        comment.Body.Should().Be("New comment");
    }
}
