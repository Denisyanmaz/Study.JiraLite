using JiraLite.Application.DTOs.Common;
using JiraLite.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace JiraLite.Application.DTOs.Task;

public sealed class TaskQueryDto : TasksPagedQueryDto
{
    public JiraTaskStatus? Status { get; set; }

    [Range(1, 5)]
    public int? Priority { get; set; }

    public Guid? AssigneeId { get; set; }

    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
}
