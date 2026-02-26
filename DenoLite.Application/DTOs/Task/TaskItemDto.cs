using DenoLite.Application.Validation;
using DenoLite.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DenoLite.Application.DTOs.Task
{
    public class TaskItemDto
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DenoTaskStatus Status { get; set; } = DenoTaskStatus.Todo;

        [Range(1, 5)]
        public int Priority { get; set; } = 3; // 1=High, 5=Low

        [Required]
        public Guid AssigneeId { get; set; }

        [Required]
        public Guid ProjectId { get; set; }

        [FutureDate]
        public DateTime? DueDate { get; set; }

        /// <summary>Optional. When set, task is placed in this board column and Status is derived from column name.</summary>
        public Guid? BoardColumnId { get; set; }
    }
}
