using JiraLite.Domain.Common;
using JiraLite.Domain.Enums;

namespace JiraLite.Domain.Entities
{
    public class TaskItem : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public JiraTaskStatus Status { get; set; } = JiraTaskStatus.Todo;
        public int Priority { get; set; } = 3; // 1=High, 5=Low
        public Guid AssigneeId { get; set; }
        public Guid ProjectId { get; set; }
        public DateTime? DueDate { get; set; }

        // ✅ Soft delete fields
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public Guid? DeletedBy { get; set; }
    }
}
