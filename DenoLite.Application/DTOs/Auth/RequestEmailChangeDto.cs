using System.ComponentModel.DataAnnotations;

namespace DenoLite.Application.DTOs.Auth
{
    public class RequestEmailChangeDto
    {
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(320)]
        public string NewEmail { get; set; } = string.Empty;
    }
}
