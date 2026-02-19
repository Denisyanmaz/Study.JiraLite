using DenoLite.Domain.Common;

namespace DenoLite.Domain.Entities
{
    public class TaskComment : BaseEntity
    {
        public Guid TaskId { get; set; }

        // Keep this explicit even though BaseEntity has CreatedBy
        // It makes intent clear and is useful for queries/authorization.
        public Guid AuthorId { get; set; }

        public string Body { get; set; } = string.Empty;

        // Optional navigation properties (recommended)
        public TaskItem Task { get; set; } = default!;
    }
}
