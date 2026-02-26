using DenoLite.Domain.Common;

namespace DenoLite.Domain.Entities
{
    /// <summary>
    /// A customizable column on the project board (e.g. Todo, In Progress, Done).
    /// </summary>
    public class BoardColumn : BaseEntity
    {
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }

        public Project Project { get; set; } = null!;
    }
}
