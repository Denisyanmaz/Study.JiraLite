using System.ComponentModel.DataAnnotations;

namespace JiraLite.Application.DTOs.Common
{
    public class ActivityFilterQueryDto : ActivityPagedQueryDto
    {
        // exact match (ex: TaskCreated, TaskUpdated, CommentAdded)
        [StringLength(100)]
        public string? ActionType { get; set; }

        public Guid? TaskId { get; set; }

        public Guid? ActorId { get; set; }

        [StringLength(100)]
        public string? Q { get; set; }
    }
}
