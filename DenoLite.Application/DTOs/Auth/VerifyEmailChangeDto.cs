using System.ComponentModel.DataAnnotations;

namespace DenoLite.Application.DTOs.Auth
{
    public class VerifyEmailChangeDto
    {
        [Required]
        [EmailAddress]
        [StringLength(320)]
        public string NewEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;
    }
}
