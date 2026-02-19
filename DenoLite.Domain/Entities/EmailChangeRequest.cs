using DenoLite.Domain.Common;

namespace DenoLite.Domain.Entities
{
    public class EmailChangeRequest : BaseEntity
    {
        public Guid UserId { get; set; }
        public string NewEmail { get; set; } = string.Empty;
        public string CodeHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
