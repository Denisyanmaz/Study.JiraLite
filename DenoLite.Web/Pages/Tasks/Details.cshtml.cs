using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.Comment;
using DenoLite.Application.DTOs.Common;
using DenoLite.Application.DTOs.ProjectMember;
using DenoLite.Application.DTOs.Task;
using DenoLite.Application.DTOs.BoardColumn;
using DenoLite.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DenoLite.Web.Pages.Tasks
{
    [IgnoreAntiforgeryToken]
    public class DetailsModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public DetailsModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // -------------------- Route / Query --------------------
        [FromRoute(Name = "id")]
        public Guid Id { get; set; } // taskId

        [FromQuery(Name = "tab")]
        public string Tab { get; set; } = "overview"; // overview | comments | activity

        // -------------------- View Data --------------------
        public TaskItem? Task { get; private set; }
        public string? Error { get; private set; }

        // Assignee dropdown (email text, guid value)
        public Dictionary<Guid, string?> AssigneeEmailById { get; private set; } = new();
        public List<SelectListItem> AssigneeSelectItems { get; private set; } = new();
        public string? AssigneesError { get; private set; } // optional debugging

        public List<TaskCommentDto> Comments { get; private set; } = new();
        public PagedResult<ActivityLogDto>? Activity { get; private set; }

        /// <summary>Board columns for the task's project. Used on overview to show Column dropdown.</summary>
        public List<BoardColumnDto> BoardColumns { get; private set; } = new();

        // -------------------- Add Comment --------------------
        [BindProperty]
        public AddCommentInputModel AddCommentInput { get; set; } = new();

        public string? CommentError { get; private set; }

        // ============================================================
        // GET
        // ============================================================
        public async Task<IActionResult> OnGetAsync() => await LoadAsync();

        // ============================================================
        // POST: Add Comment
        // ============================================================
        public async Task<IActionResult> OnPostAddCommentAsync()
        {
            Tab = "comments";

            if (Id == Guid.Empty)
            {
                Error = "Invalid task id.";
                return Page();
            }

            if (!ModelState.IsValid)
            {
                CommentError = BuildModelStateError(ModelState);
                await LoadAsync(loadTask: true, loadComments: true, loadActivity: false);
                return Page();
            }

            var client = _httpClientFactory.CreateClient("DenoLiteApi");

            var dto = new CreateCommentDto
            {
                Body = AddCommentInput.Body
            };

            var resp = await client.PostAsJsonAsync($"/api/comments/task/{Id}", dto);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToPage("/Login");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                CommentError = $"Add comment failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}";

                await LoadAsync(loadTask: true, loadComments: true, loadActivity: false);
                return Page();
            }

            return Redirect($"/Tasks/Details/{Id}?tab=comments");
        }

        // ============================================================
        // POST: Inline Update (full task dto)
        // Called by JS: POST ?handler=Update
        // ============================================================
        public async Task<IActionResult> OnPostUpdateAsync()
        {
            Tab = "overview";

            if (Id == Guid.Empty)
                return BadRequest("Invalid task id.");

            var dto = await Request.ReadFromJsonAsync<TaskItemDto>(_jsonOptions);
            if (dto == null)
                return BadRequest("Missing body.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");

            var resp = await client.PutAsJsonAsync($"/api/tasks/{Id}", dto);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return Unauthorized();

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, body);
            }

            return new JsonResult(new { ok = true });
        }
        public async Task<IActionResult> OnPostSaveFullUpdateAsync()
        {
            if (Id == Guid.Empty)
                return BadRequest("Invalid task id.");

            var dto = await Request.ReadFromJsonAsync<TaskItemDto>(_jsonOptions);
            if (dto == null)
                return BadRequest("Missing body.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");

            var resp = await client.PutAsJsonAsync($"/api/tasks/{Id}", dto);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToPage("/Login");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, body);
            }

            return new JsonResult(new { redirectUrl = $"/Projects/Details/{dto.ProjectId}?tab=board" });
        }



        // ============================================================
        // POST: Inline Status Update
        // Called by JS: POST ?handler=UpdateStatus
        // ============================================================
        public async Task<IActionResult> OnPostUpdateStatusAsync()
        {
            Tab = "overview";

            if (Id == Guid.Empty)
                return BadRequest("Invalid task id.");

            var dto = await Request.ReadFromJsonAsync<UpdateTaskStatusDto>(_jsonOptions);
            if (dto == null)
                return BadRequest("Missing body.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");

            var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/tasks/{Id}/status")
            {
                Content = JsonContent.Create(dto)
            };

            var resp = await client.SendAsync(req);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return Unauthorized();

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, body);
            }

            return new JsonResult(new { ok = true });
        }

        // ============================================================
        // POST: Delete Task
        // Called by JS: POST ?handler=DeleteTask
        // ============================================================
        public async Task<IActionResult> OnPostDeleteTaskAsync()
        {
            if (Id == Guid.Empty)
                return BadRequest("Invalid task id.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");

            var resp = await client.DeleteAsync($"/api/tasks/{Id}");

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return Unauthorized();

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, body);
            }

            return new JsonResult(new { ok = true });
        }

        // ============================================================
        // Loader
        // ============================================================
        private async Task<IActionResult> LoadAsync(
            bool loadTask = true,
            bool loadComments = true,
            bool loadActivity = true)
        {
            if (Id == Guid.Empty)
            {
                Error = "Invalid task id.";
                return Page();
            }

            var client = _httpClientFactory.CreateClient("DenoLiteApi");

            // 1) Task
            if (loadTask)
            {
                var taskResp = await client.GetAsync($"/api/tasks/{Id}");

                if (taskResp.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToPage("/Login");

                if (!taskResp.IsSuccessStatusCode)
                {
                    var body = await taskResp.Content.ReadAsStringAsync();
                    Error = $"API error (task): {(int)taskResp.StatusCode} {taskResp.ReasonPhrase}\n{body}";
                    return Page();
                }

                Task = await taskResp.Content.ReadFromJsonAsync<TaskItem>();
                if (Task == null)
                {
                    Error = "Task not found.";
                    return Page();
                }

                // ✅ important: only after Task is loaded
                await LoadAssigneesAsync(client, Task.ProjectId);
                await LoadBoardColumnsAsync(client, Task.ProjectId);
            }

            // 2) Comments (only on comments tab)
            if (loadComments && string.Equals(Tab, "comments", StringComparison.OrdinalIgnoreCase))
            {
                var cResp = await client.GetAsync($"/api/comments/task/{Id}");

                if (cResp.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToPage("/Login");

                if (cResp.IsSuccessStatusCode)
                {
                    Comments = await cResp.Content.ReadFromJsonAsync<List<TaskCommentDto>>() ?? new();
                }
                else
                {
                    var body = await cResp.Content.ReadAsStringAsync();
                    CommentError ??= $"API error (comments): {(int)cResp.StatusCode} {cResp.ReasonPhrase}\n{body}";
                }
            }

            // 3) Activity (only on activity tab)
            if (loadActivity && string.Equals(Tab, "activity", StringComparison.OrdinalIgnoreCase))
            {
                var aResp = await client.GetAsync($"/api/activity/task/{Id}?page=1&pageSize=50");

                if (aResp.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToPage("/Login");

                if (aResp.IsSuccessStatusCode)
                {
                    Activity = await aResp.Content.ReadFromJsonAsync<PagedResult<ActivityLogDto>>();
                    
                    // Load actor emails for all actors in activity logs
                    if (Activity?.Items != null && Activity.Items.Count > 0 && Task != null)
                    {
                        var actorIds = Activity.Items.Select(a => a.ActorId).Distinct().ToList();
                        await LoadActorEmailsAsync(client, actorIds, Task.ProjectId);
                    }
                }
            }

            return Page();
        }

        // ============================================================
        // Actor emails loader (for activity logs)
        // ============================================================
        private async Task LoadActorEmailsAsync(HttpClient client, List<Guid> actorIds, Guid projectId)
        {
            // First, load all members (including removed ones for owners) - this covers most actors
            try
            {
                var membersResp = await client.GetAsync($"/api/projects/{projectId}/members");
                if (membersResp.IsSuccessStatusCode)
                {
                    var members = await membersResp.Content.ReadFromJsonAsync<List<ProjectMemberDto>>() ?? new();
                    foreach (var member in members)
                    {
                        if (!string.IsNullOrWhiteSpace(member.Email))
                        {
                            AssigneeEmailById[member.UserId] = member.Email;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            // For any remaining actor IDs, try to extract email from activity log messages
            foreach (var actorId in actorIds)
            {
                if (AssigneeEmailById.ContainsKey(actorId))
                    continue; // Already loaded

                // Try to extract email from activity messages
                if (Activity?.Items != null)
                {
                    var actorActivities = Activity.Items.Where(a => a.ActorId == actorId).ToList();
                    foreach (var activity in actorActivities)
                    {
                        // Extract email from messages like "Member added: email@example.com (UserId: ...)"
                        var emailMatch = System.Text.RegularExpressions.Regex.Match(
                            activity.Message ?? "",
                            @"(?:Member\s+(?:added|removed|left):\s*)?([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        if (emailMatch.Success && emailMatch.Groups.Count > 1)
                        {
                            var email = emailMatch.Groups[1].Value;
                            if (!string.IsNullOrWhiteSpace(email))
                            {
                                AssigneeEmailById[actorId] = email;
                                break; // Found email, move to next actor
                            }
                        }
                    }
                }
            }
        }

        // ============================================================
        // Assignees loader (members -> dropdown)
        // ============================================================
        private async Task LoadAssigneesAsync(HttpClient client, Guid projectId)
        {
            AssigneeSelectItems = new List<SelectListItem>();
            AssigneeEmailById = new Dictionary<Guid, string?>();

            try
            {
                var resp = await client.GetAsync($"/api/projects/{projectId}/members");
                if (!resp.IsSuccessStatusCode)
                {
                    AssigneesError = $"Could not load members for assignees. Status: {(int)resp.StatusCode}";
                    return;
                }

                var members = await resp.Content.ReadFromJsonAsync<List<ProjectMemberDto>>() ?? new();

                // Only allow assigning to ACTIVE members
                members = members.Where(m => !m.IsRemoved).ToList();

                var ordered = members
                    .OrderByDescending(m => string.Equals(m.Role, "Owner", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(m => m.Email ?? m.UserId.ToString());

                foreach (var m in ordered)
                {
                    AssigneeEmailById[m.UserId] = m.Email;

                    var label = string.IsNullOrWhiteSpace(m.Email)
                        ? $"{m.UserId} ({m.Role})"
                        : $"{m.Email} ({m.Role})";

                    AssigneeSelectItems.Add(new SelectListItem
                    {
                        Value = m.UserId.ToString(), // GUID
                        Text = label                // Email (Role)
                    });
                }
            }
            catch (Exception ex)
            {
                // optional debug
                AssigneesError = ex.Message;
            }
        }

        private async Task LoadBoardColumnsAsync(HttpClient client, Guid projectId)
        {
            BoardColumns = new List<BoardColumnDto>();
            try
            {
                var resp = await client.GetAsync($"/api/projects/{projectId}/board-columns");
                if (resp.IsSuccessStatusCode)
                {
                    var list = await resp.Content.ReadFromJsonAsync<List<BoardColumnDto>>();
                    BoardColumns = list ?? new List<BoardColumnDto>();
                }
            }
            catch
            {
                // ignore
            }
        }

        private static string BuildModelStateError(ModelStateDictionary modelState)
        {
            var sb = new StringBuilder();
            foreach (var kvp in modelState)
            {
                foreach (var err in kvp.Value.Errors)
                    sb.AppendLine($"{kvp.Key}: {err.ErrorMessage}");
            }
            return sb.Length == 0 ? "ModelState invalid (unknown reason)." : sb.ToString();
        }

        // Helper to clean User IDs from old activity log messages
        public string CleanActivityMessage(string message, string actionType)
        {
            if (string.IsNullOrWhiteSpace(message))
                return message;

            // Remove User ID patterns from member-related actions
            if (actionType == "MemberAdded" || actionType == "MemberRemoved" || actionType == "MemberLeft")
            {
                // Remove patterns like "(UserId: guid)" or "(Userid: guid)" or "(Userld: guid)" (case insensitive, handles typos)
                // Matches: User followed by any characters, then : or space, then GUID in parentheses
                message = System.Text.RegularExpressions.Regex.Replace(
                    message,
                    @"\s*\(User[^:)]*[:\s]+[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\)",
                    "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return message.Trim();
        }

        public sealed class AddCommentInputModel
        {
            [Required]
            [StringLength(400, MinimumLength = 1)]
            public string Body { get; set; } = "";
        }
    }
}
