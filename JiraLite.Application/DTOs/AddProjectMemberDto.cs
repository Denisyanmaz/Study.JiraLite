using System.ComponentModel.DataAnnotations;

public class AddProjectMemberDto
{
    [Required]
    public Guid UserId { get; set; }

    public string Role { get; set; } = "Member";
}
