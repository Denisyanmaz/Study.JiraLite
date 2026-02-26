using System.ComponentModel.DataAnnotations;

namespace DenoLite.Application.DTOs.Task
{
    public class AddTaskTagDto
    {
        [Required]
        [StringLength(20, MinimumLength = 1)]
        public string Label { get; set; } = "";

        [Required]
        [StringLength(30)]
        public string Color { get; set; } = "#6c757d";
    }
}
