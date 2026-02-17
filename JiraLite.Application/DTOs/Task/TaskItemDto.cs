using JiraLite.Application.Validation;
using JiraLite.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace JiraLite.Application.DTOs.Task
{
    public class TaskItemDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public JiraTaskStatus Status { get; set; } = JiraTaskStatus.Todo;

        [Range(1, 5)]
        public int Priority { get; set; } = 3; // 1=High, 5=Low

        [Required]
        public Guid AssigneeId { get; set; }

        [Required]
        public Guid ProjectId { get; set; }

        [FutureDate]
        public DateTime? DueDate { get; set; }
    }
}
