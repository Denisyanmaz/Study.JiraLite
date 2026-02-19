using System.ComponentModel.DataAnnotations;

namespace DenoLite.Application.DTOs.ProjectMember
{
    public class ProjectMemberDto
    {
        [Required]
        public Guid UserId { get; set; }   // invite user id
        public string? Email { get; set; }

        [Required]
        [RegularExpression("^(Owner|Member)$", ErrorMessage = "Role must be 'Owner' or 'Member'.")]
        public string Role { get; set; } = "Member";

        /// <summary>
        /// True if this member was removed/left the project (for display purposes)
        /// </summary>
        public bool IsRemoved { get; set; } = false;
    }
}
