using JiraLite.Domain.Common;

namespace JiraLite.Domain.Entities
{
    public class ActivityLog : BaseEntity
    {
        public Guid ProjectId { get; set; }
        public Guid? TaskId { get; set; }

        // Who performed the action (domain intent).
        // Typically same as CreatedBy.
        public Guid ActorId { get; set; }

        // e.g. "TaskCreated", "TaskUpdated", "CommentAdded"
        public string ActionType { get; set; } = string.Empty;

        // Human-readable text for UI
        public string Message { get; set; } = string.Empty;
    }
}
