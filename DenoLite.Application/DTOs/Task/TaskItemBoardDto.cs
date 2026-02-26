using System.Text.Json.Serialization;
using DenoLite.Domain.Enums;

namespace DenoLite.Application.DTOs.Task
{
    /// <summary>
    /// Task DTO for board/list with assignee email (including assignees who left the project).
    /// </summary>
    public class TaskItemBoardDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DenoTaskStatus Status { get; set; }

        public int Priority { get; set; }
        public Guid AssigneeId { get; set; }
        /// <summary>Assignee email from Users; null if user not found.</summary>
        public string? AssigneeEmail { get; set; }
        public Guid ProjectId { get; set; }
        public DateTime? DueDate { get; set; }
        /// <summary>User-defined tags for this task.</summary>
        public List<TaskTagDto> Tags { get; set; } = new();
    }
}
