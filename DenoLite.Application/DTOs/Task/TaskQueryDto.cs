using DenoLite.Application.DTOs.Common;
using DenoLite.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace DenoLite.Application.DTOs.Task;

public sealed class TaskQueryDto : TasksPagedQueryDto
{
    public DenoTaskStatus? Status { get; set; }

    [Range(1, 5)]
    public int? Priority { get; set; }

    public Guid? AssigneeId { get; set; }

    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
}
