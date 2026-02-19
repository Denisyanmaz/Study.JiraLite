using DenoLite.Domain.Enums;

namespace DenoLite.Application.DTOs.Task
{
    public class UpdateTaskStatusDto
    {
        public DenoTaskStatus Status { get; set; }
    }

}
