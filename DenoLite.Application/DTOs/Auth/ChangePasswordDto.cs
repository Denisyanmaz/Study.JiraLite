using System.ComponentModel.DataAnnotations;

namespace DenoLite.Application.DTOs.Auth
{
    public class ChangePasswordDto
    {
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string OldPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
