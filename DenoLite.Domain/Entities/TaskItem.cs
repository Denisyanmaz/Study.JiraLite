using DenoLite.Domain.Common;
using DenoLite.Domain.Enums;

namespace DenoLite.Domain.Entities
{
    public class TaskItem : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DenoTaskStatus Status { get; set; } = DenoTaskStatus.Todo;
        public int Priority { get; set; } = 3; // 1=High, 5=Low
        public Guid AssigneeId { get; set; }
        public Guid ProjectId { get; set; }
        public DateTime? DueDate { get; set; }

        // ? Soft delete fields
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public Guid? DeletedBy { get; set; }
    }
}
