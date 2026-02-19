using FluentAssertions;
using DenoLite.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DenoLite.Tests.Integration;

public class DbConstraintTests : TestBase
{
    public DbConstraintTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ProjectMembers_Has_Unique_Constraint_On_ProjectId_UserId()
    {
        var (owner, member, project) = await SeedProjectWithMemberAsync();

        // already seeded one membership for member in SeedProjectWithMemberAsync
        // attempt to insert duplicate membership directly (bypassing service)
        Db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = member.Id,
            Role = "Member"
        });

        Func<Task> act = async () => await Db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
