using DenoLite.Domain.Common;

namespace DenoLite.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? GoogleId { get; set; } // For OAuth users
        public string Role { get; set; } = "User"; // Admin, User
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
        /// <summary>When false, user does not receive notification emails (comments, assignments, mentions). Transactional emails (verification, password reset) are always sent.</summary>
        public bool NotificationsEnabled { get; set; } = true;
    }
}
