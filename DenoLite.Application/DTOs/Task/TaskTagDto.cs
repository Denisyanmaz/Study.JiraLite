namespace DenoLite.Application.DTOs.Task
{
    public class TaskTagDto
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Color { get; set; } = "#6c757d";
    }
}
