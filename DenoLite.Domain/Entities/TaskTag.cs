using DenoLite.Domain.Common;

namespace DenoLite.Domain.Entities
{
    public class TaskTag : BaseEntity
    {
        public Guid TaskId { get; set; }
        /// <summary>Display label; max 20 characters.</summary>
        public string Label { get; set; } = string.Empty;
        /// <summary>Color as hex (e.g. #ff0000) or CSS color name.</summary>
        public string Color { get; set; } = "#6c757d";
    }
}
