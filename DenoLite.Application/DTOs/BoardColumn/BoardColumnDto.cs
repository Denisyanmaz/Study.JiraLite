namespace DenoLite.Application.DTOs.BoardColumn
{
    public class BoardColumnDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int TaskCount { get; set; }
    }
}
