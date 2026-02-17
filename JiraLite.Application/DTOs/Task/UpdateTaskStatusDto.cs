using JiraLite.Domain.Enums;

namespace JiraLite.Application.DTOs.Task
{
    public class UpdateTaskStatusDto
    {
        public JiraTaskStatus Status { get; set; }
    }

}
