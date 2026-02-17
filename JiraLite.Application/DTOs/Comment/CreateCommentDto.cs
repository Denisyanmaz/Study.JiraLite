using System.ComponentModel.DataAnnotations;

namespace JiraLite.Application.DTOs.Comment
{
    public class CreateCommentDto
    {
        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Body { get; set; } = string.Empty;
    }
}
