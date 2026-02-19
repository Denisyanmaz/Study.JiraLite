using DenoLite.Domain.Common;

namespace DenoLite.Domain.Entities
{
    public class ProjectMember : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public string Role { get; set; } = "Member"; // Owner or Member
    }
}
