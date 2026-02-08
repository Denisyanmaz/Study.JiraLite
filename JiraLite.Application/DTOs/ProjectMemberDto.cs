using System;
using System.Collections.Generic;
using System.Text;

namespace JiraLite.Application.DTOs
{
    public class ProjectMemberDto
    {
        public Guid UserId { get; set; }       // ID of the user to invite
        public string Role { get; set; } = "Member";  // "Owner" or "Member"
    }

}
