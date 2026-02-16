using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Common;
using JiraLite.Domain.Entities;
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

namespace JiraLite.Web.Pages.Tasks
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

        [FromRoute(Name = "id")]
        public Guid Id { get; set; } // taskId

        [FromQuery(Name = "tab")]
        public string Tab { get; set; } = "overview"; // overview | comments | activity

        public TaskItem? Task { get; private set; }
        public string? Error { get; private set; }

        public List<TaskCommentDto> Comments { get; private set; } = new();
        public PagedResult<ActivityLogDto>? Activity { get; private set; }

        // ----- Add Comment form -----
        [BindProperty]
        public AddCommentInputModel AddCommentInput { get; set; } = new();

        public string? CommentError { get; private set; }

        public async Task<IActionResult> OnGetAsync()
            => await LoadAsync();
        private async Task LoadAssigneesAsync(HttpClient client)
        {
            AssigneeSelectItems = new List<SelectListItem>();

            if (Task == null || Task.ProjectId == Guid.Empty)
                return;

            try
            {
                var resp = await client.GetAsync($"/api/projects/{Task.ProjectId}/members");
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    AssigneesError = $"Assignees load failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}";
                    return;
                }

                var members = await resp.Content.ReadFromJsonAsync<List<ProjectMemberDto>>() ?? new();

                // Owner first then by UserId (nice UX)
                var ordered = members
                    .OrderByDescending(m => string.Equals(m.Role, "Owner", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(m => m.UserId);

                foreach (var m in ordered)
                {
                    AssigneeSelectItems.Add(new SelectListItem
                    {
                        Value = m.UserId.ToString(),
                        Text = $"{m.UserId} ({m.Role})"
                    });
                }
            }
            catch (Exception ex)
            {
                AssigneesError = $"Assignees load exception: {ex.Message}";
            }
        }

        public List<SelectListItem> AssigneeSelectItems { get; private set; } = new();
        public string? AssigneesError { get; private set; } // optional, for debugging UI

        public async Task<IActionResult> OnPostAddCommentAsync()
        {
            // Always stay on comments tab for this POST
            Tab = "comments";

            if (Id == Guid.Empty)
            {
                Error = "Invalid task id.";
                return Page();
            }

            // If model binding/validation fails, show why
            if (!ModelState.IsValid)
            {
                CommentError = BuildModelStateError(ModelState);

                await LoadAsync(loadTask: true, loadComments: true, loadActivity: false);
                return Page();
            }

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

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

            // PRG pattern (Prevents duplicate post on refresh)
            return Redirect($"/Tasks/Details/{Id}?tab=comments");
        }

        // ============================
        // NEW: Inline update handlers
        // ============================

        // Called by JS: POST ?handler=Update
        public async Task<IActionResult> OnPostUpdateAsync()
        {
            Tab = "overview";

            if (Id == Guid.Empty)
                return BadRequest("Invalid task id.");

            // Read TaskItemDto JSON from request body
            var dto = await Request.ReadFromJsonAsync<TaskItemDto>(_jsonOptions);

            if (dto == null)
                return BadRequest("Missing body.");

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            // Forward to API
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

        // Called by JS: POST ?handler=UpdateStatus
        public async Task<IActionResult> OnPostUpdateStatusAsync()
        {
            Tab = "overview";

            if (Id == Guid.Empty)
                return BadRequest("Invalid task id.");

            var dto = await Request.ReadFromJsonAsync<UpdateTaskStatusDto>(_jsonOptions);


            if (dto == null)
                return BadRequest("Missing body.");

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            // PatchAsJsonAsync isn't always available depending on target/framework,
            // so we do a manual PATCH request safely:
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

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

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
                await LoadAssigneesAsync(client);
            }

            // 2) Comments (load on comments tab)
            if (loadComments && Tab == "comments")
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
                    // if comments endpoint fails, show it (optional)
                    var body = await cResp.Content.ReadAsStringAsync();
                    CommentError ??= $"API error (comments): {(int)cResp.StatusCode} {cResp.ReasonPhrase}\n{body}";
                }
            }

            // 3) Activity (paged)
            if (loadActivity && Tab == "activity")
            {
                var aResp = await client.GetAsync($"/api/activity/task/{Id}?page=1&pageSize=50");
                if (aResp.StatusCode == HttpStatusCode.Unauthorized)
                    return RedirectToPage("/Login");

                if (aResp.IsSuccessStatusCode)
                {
                    Activity = await aResp.Content.ReadFromJsonAsync<PagedResult<ActivityLogDto>>();
                }
            }

            return Page();
        }

        private static string BuildModelStateError(ModelStateDictionary modelState)
        {
            var sb = new StringBuilder();
            foreach (var kvp in modelState)
            {
                foreach (var err in kvp.Value.Errors)
                {
                    sb.AppendLine($"{kvp.Key}: {err.ErrorMessage}");
                }
            }

            return sb.Length == 0 ? "ModelState invalid (unknown reason)." : sb.ToString();
        }

        public sealed class AddCommentInputModel
        {
            [Required]
            [StringLength(2000, MinimumLength = 1)]
            public string Body { get; set; } = "";
        }
    }
}
