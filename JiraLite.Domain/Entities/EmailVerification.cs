using JiraLite.Domain.Common;

namespace JiraLite.Domain.Entities
{
    public class EmailVerification : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        // Store only hash (never store raw code)
        public string CodeHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; } = 0;

        // For rate-limit resend
        public DateTime LastSentAt { get; set; }
        public int SendCount { get; set; } = 1;

        // optional: if you ever want to invalidate without deleting
        public bool IsUsed { get; set; } = false;
    }
}
