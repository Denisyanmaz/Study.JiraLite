using System.ComponentModel.DataAnnotations;

namespace JiraLite.Application.DTOs.Auth
{
    public class RegisterUserDto
    {
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;
    }
}
