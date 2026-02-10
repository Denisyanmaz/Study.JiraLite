namespace JiraLite.Application.DTOs
{
    public class ActivityLogDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? TaskId { get; set; }
        public Guid ActorId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
