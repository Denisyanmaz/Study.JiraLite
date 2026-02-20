using System.ComponentModel.DataAnnotations;

namespace DenoLite.Application.DTOs.ProjectMember
{
    public class AddProjectMemberDto
    {
        [Required]
        public Guid UserId { get; set; }

        public string Role { get; set; } = "Member";
    }
}
