using JiraLite.Domain.Common;

namespace JiraLite.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "User"; // Admin, User
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
    }
}
