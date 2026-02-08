using JiraLite.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace JiraLite.Application.DTOs
{
    public class TaskItemDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public JiraTaskStatus Status { get; set; } = JiraTaskStatus.Todo;

        [Range(1, 5)]
        public int Priority { get; set; } = 3;

        [Required]
        public Guid AssigneeId { get; set; }

        [Required]
        public Guid ProjectId { get; set; }

        public DateTime? DueDate { get; set; }
    }
}
