using System;
using System.Collections.Generic;
using System.Text;

namespace JiraLite.Application.DTOs.Task
{
    public class TaskCommentDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid AuthorId { get; set; }
        public string Body { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
