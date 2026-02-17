namespace JiraLite.Application.DTOs.Comment
{
    public class CommentDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid AuthorId { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
