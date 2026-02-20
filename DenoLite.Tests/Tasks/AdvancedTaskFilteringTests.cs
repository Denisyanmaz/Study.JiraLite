using FluentAssertions;
using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.Task;
using DenoLite.Domain.Entities;
using DenoLite.Domain.Enums;
using DenoLite.Tests.Integration;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DenoLite.Tests.Tasks;

public class AdvancedTaskFilteringTests : TestBase
{
    public AdvancedTaskFilteringTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Filter_By_Priority_Returns_Correct_Tasks()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // Create tasks with different priorities
        var highPriorityTasks = Enumerable.Range(0, 3).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = member.Id,
            Title = $"High Priority {i}",
            Status = DenoTaskStatus.Todo,
            Priority = 1, // High priority
            DueDate = DateTime.UtcNow.AddDays(1)
        });

        var lowPriorityTasks = Enumerable.Range(0, 5).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = member.Id,
            Title = $"Low Priority {i}",
            Status = DenoTaskStatus.Todo,
            Priority = 5, // Low priority
            DueDate = DateTime.UtcNow.AddDays(1)
        });

        Db.Tasks.AddRange(highPriorityTasks);
        Db.Tasks.AddRange(lowPriorityTasks);
        await Db.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?priority=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(3);
        payload.Items.Should().OnlyContain(t => t.Priority == 1);
    }

    [Fact]
    public async Task Filter_By_Assignee_Returns_Correct_Tasks()
    {
        // Arrange
        var owner = TestHelpers.CreateUser("owner@test.com");
        var assignee1 = TestHelpers.CreateUser("assignee1@test.com");
        var assignee2 = TestHelpers.CreateUser("assignee2@test.com");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Filter Project",
            OwnerId = owner.Id
        };

        Db.Users.AddRange(owner, assignee1, assignee2);
        Db.Projects.Add(project);
        Db.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = "Owner" },
            new ProjectMember { ProjectId = project.Id, UserId = assignee1.Id, Role = "Member" },
            new ProjectMember { ProjectId = project.Id, UserId = assignee2.Id, Role = "Member" }
        );
        await Db.SaveChangesAsync();

        // Create tasks for different assignees
        var tasksForAssignee1 = Enumerable.Range(0, 4).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = assignee1.Id,
            Title = $"Task for Assignee1 {i}",
            Status = DenoTaskStatus.Todo,
            Priority = 3,
            DueDate = DateTime.UtcNow.AddDays(1)
        });

        var tasksForAssignee2 = Enumerable.Range(0, 2).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = assignee2.Id,
            Title = $"Task for Assignee2 {i}",
            Status = DenoTaskStatus.Todo,
            Priority = 3,
            DueDate = DateTime.UtcNow.AddDays(1)
        });

        Db.Tasks.AddRange(tasksForAssignee1);
        Db.Tasks.AddRange(tasksForAssignee2);
        await Db.SaveChangesAsync();

        SetAuth(owner);

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?assigneeId={assignee1.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(4);
        payload.Items.Should().OnlyContain(t => t.AssigneeId == assignee1.Id);
    }

    [Fact]
    public async Task Filter_By_DueDate_Range_Returns_Correct_Tasks()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        var dueDateFrom = DateTime.UtcNow.AddDays(5);
        var dueDateTo = DateTime.UtcNow.AddDays(10);

        // Tasks within range
        var tasksInRange = Enumerable.Range(0, 3).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = member.Id,
            Title = $"Task in range {i}",
            Status = DenoTaskStatus.Todo,
            Priority = 3,
            DueDate = dueDateFrom.AddDays(i) // Within range
        });

        // Tasks outside range
        var tasksOutOfRange = Enumerable.Range(0, 2).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = member.Id,
            Title = $"Task out of range {i}",
            Status = DenoTaskStatus.Todo,
            Priority = 3,
            DueDate = DateTime.UtcNow.AddDays(20) // Outside range
        });

        Db.Tasks.AddRange(tasksInRange);
        Db.Tasks.AddRange(tasksOutOfRange);
        await Db.SaveChangesAsync();

        // Act
        var fromDate = dueDateFrom.ToString("yyyy-MM-dd");
        var toDate = dueDateTo.ToString("yyyy-MM-dd");
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?dueFrom={fromDate}&dueTo={toDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(3);
        
        // Compare dates only (API parses "yyyy-MM-dd" as midnight UTC, so compare date components)
        var fromDateOnly = dueDateFrom.Date;
        var toDateOnly = dueDateTo.Date;
        payload.Items.Should().OnlyContain(t => 
            t.DueDate.HasValue && 
            t.DueDate.Value.Date >= fromDateOnly && 
            t.DueDate.Value.Date <= toDateOnly);
    }

    [Fact]
    public async Task Filter_By_Multiple_Criteria_Works()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // Tasks matching all criteria: Status=InProgress, Priority=2, Assignee=member
        var matchingTasks = Enumerable.Range(0, 2).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = member.Id,
            Title = $"Matching Task {i}",
            Status = DenoTaskStatus.InProgress,
            Priority = 2,
            DueDate = DateTime.UtcNow.AddDays(5)
        });

        // Tasks not matching all criteria
        var nonMatchingTasks = new[]
        {
            new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                AssigneeId = member.Id,
                Title = "Wrong Status",
                Status = DenoTaskStatus.Todo, // Wrong status
                Priority = 2,
                DueDate = DateTime.UtcNow.AddDays(5)
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                AssigneeId = member.Id,
                Title = "Wrong Priority",
                Status = DenoTaskStatus.InProgress,
                Priority = 5, // Wrong priority
                DueDate = DateTime.UtcNow.AddDays(5)
            }
        };

        Db.Tasks.AddRange(matchingTasks);
        Db.Tasks.AddRange(nonMatchingTasks);
        await Db.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?status=InProgress&priority=2&assigneeId={member.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(2);
        payload.Items.Should().OnlyContain(t =>
            t.Status == DenoTaskStatus.InProgress &&
            t.Priority == 2 &&
            t.AssigneeId == member.Id);
    }

    [Fact]
    public async Task Filter_With_No_Results_Returns_Empty_List()
    {
        // Arrange
        var (owner, member, project) = await SeedProjectWithMemberAsync();
        SetAuth(member);

        // Create tasks with different status
        var tasks = Enumerable.Range(0, 3).Select(i => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = member.Id,
            Title = $"Task {i}",
            Status = DenoTaskStatus.Todo,
            Priority = 3,
            DueDate = DateTime.UtcNow.AddDays(1)
        });

        Db.Tasks.AddRange(tasks);
        await Db.SaveChangesAsync();

        // Act - filter for status that doesn't exist
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged?status=Done");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().BeEmpty();
        payload.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Filter_By_Assignee_Shows_Email_For_Departed_Member()
    {
        // Arrange
        var owner = TestHelpers.CreateUser("owner@test.com");
        var departedMember = TestHelpers.CreateUser("departed@test.com");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            OwnerId = owner.Id
        };

        Db.Users.AddRange(owner, departedMember);
        Db.Projects.Add(project);
        // Note: departedMember is NOT a project member anymore
        Db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = "Owner" });
        await Db.SaveChangesAsync();

        // Create task assigned to departed member
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AssigneeId = departedMember.Id, // Assigned to departed member
            Title = "Task for departed member",
            Status = DenoTaskStatus.Todo,
            Priority = 3,
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        Db.Tasks.Add(task);
        await Db.SaveChangesAsync();

        SetAuth(owner);

        // Act
        var response = await Client.GetAsync($"/api/tasks/project/{project.Id}/paged");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TaskItemBoardDto>>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(1);
        payload.Items[0].AssigneeEmail.Should().Be(departedMember.Email);
        payload.Items[0].AssigneeId.Should().Be(departedMember.Id);
    }
}
