using System.ComponentModel.DataAnnotations;

namespace DenoLite.Application.DTOs.Comment
{
    public class CreateCommentDto
    {
        [Required]
        [StringLength(400, MinimumLength = 1)]
        public string Body { get; set; } = string.Empty;
    }
}
