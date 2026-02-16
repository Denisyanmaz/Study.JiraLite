using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Common;
using JiraLite.Domain.Entities;
using JiraLite.Domain.Enums;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace JiraLite.Web.Pages.Projects
{
    public class DetailsModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DetailsModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // -------------------- Route --------------------
        [FromRoute(Name = "id")]
        public Guid ProjectId { get; set; }

        // -------------------- Tabs --------------------
        [BindProperty(SupportsGet = true, Name = "tab")]
        public string Tab { get; set; } = "board"; // board | activity | members

        // -------------------- Board query params --------------------
        [FromQuery(Name = "status")]
        public JiraTaskStatus? Status { get; set; }

        [FromQuery(Name = "priority")]
        public int? Priority { get; set; }

        [FromQuery(Name = "assigneeId")]
        public Guid? AssigneeId { get; set; }

        [FromQuery(Name = "dueFrom")]
        public DateTime? DueFrom { get; set; }

        [FromQuery(Name = "dueTo")]
        public DateTime? DueTo { get; set; }

        [FromQuery(Name = "page")]
        public int Page { get; set; } = 1;

        [FromQuery(Name = "pageSize")]
        public int PageSize { get; set; } = 15;

        // -------------------- Activity query params --------------------
        [FromQuery(Name = "aPage")]
        public int ActivityPage { get; set; } = 1;

        [FromQuery(Name = "aSize")]
        public int ActivityPageSize { get; set; } = 50;

        [FromQuery(Name = "actionType")]
        public string? ActivityActionType { get; set; }

        [FromQuery(Name = "actorId")]
        public Guid? ActivityActorId { get; set; }

        [FromQuery(Name = "taskId")]
        public Guid? ActivityTaskId { get; set; }

        [FromQuery(Name = "q")]
        public string? ActivityQ { get; set; }

        // -------------------- Members --------------------
        public List<ProjectMemberDto> Members { get; private set; } = new();
        public string? MembersError { get; private set; }

        [BindProperty]
        public ProjectMemberDto AddMemberInput { get; set; } = new();

        // ✅ For UI permissions (Leave/Remove rendering)
        public Guid CurrentUserId { get; private set; }
        public string? CurrentUserRole { get; private set; }

        // -------------------- Create Task --------------------
        [BindProperty]
        public CreateTaskInputModel CreateTaskInput { get; set; } = new();

        public string? CreateError { get; private set; }

        // Assignee dropdown data (this is what your cshtml expects)
        public List<SelectListItem> AssigneeSelectItems { get; private set; } = new();

        // -------------------- View data --------------------
        public string ProjectName { get; private set; } = "Project";
        public string? ProjectDescription { get; private set; }

        public List<TaskItem> Tasks { get; private set; } = new();
        public List<TaskItem> TodoTasks { get; private set; } = new();
        public List<TaskItem> InProgressTasks { get; private set; } = new();
        public List<TaskItem> DoneTasks { get; private set; } = new();

        public int TotalCount { get; private set; }
        public int TotalPages =>
            PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

        public PagedResult<ActivityLogDto>? ProjectActivity { get; private set; }

        public string? Error { get; private set; }

        // ============================================================
        // GET
        // ============================================================
        public async Task<IActionResult> OnGetAsync() => await LoadPageAsync();

        // ============================================================
        // POST: Create Task
        // ============================================================
        public async Task<IActionResult> OnPostCreateTaskAsync()
        {
            Tab = "board";

            // ✅ Clear existing validation results so AddMember fields never interfere
            ModelState.Clear();

            // Fix PostgreSQL "DateTime Kind=Unspecified" for date-only UI input
            if (CreateTaskInput.DueDate.HasValue)
                CreateTaskInput.DueDate = DateTime.SpecifyKind(CreateTaskInput.DueDate.Value, DateTimeKind.Utc);

            // ✅ Validate ONLY CreateTaskInput
            if (!TryValidateModel(CreateTaskInput, nameof(CreateTaskInput)))
            {
                await LoadPageAsync();
                return Page();
            }

            if (ProjectId == Guid.Empty)
            {
                Error = "Invalid project id.";
                await LoadPageAsync();
                return Page();
            }

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            var dto = new TaskItemDto
            {
                Title = CreateTaskInput.Title,
                Description = CreateTaskInput.Description,
                Status = CreateTaskInput.Status,
                Priority = CreateTaskInput.Priority,
                ProjectId = ProjectId,
                AssigneeId = CreateTaskInput.AssigneeId,
                DueDate = CreateTaskInput.DueDate
            };

            var resp = await client.PostAsJsonAsync("/api/tasks", dto);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToPage("/Login");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                CreateError = $"Create failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}";
                await LoadPageAsync();
                return Page();
            }

            return Redirect(BoardPageLink(1));
        }

        // ============================================================
        // POST: Add Member
        // ============================================================
        public async Task<IActionResult> OnPostAddMemberAsync()
        {
            Tab = "members";

            // ✅ Clear existing validation results (prevents other form fields like "Title" from blocking)
            ModelState.Clear();

            // ✅ Validate ONLY AddMemberInput
            if (!TryValidateModel(AddMemberInput, nameof(AddMemberInput)))
            {
                var allErrors = ModelState
                    .Where(kvp => kvp.Value?.Errors?.Count > 0)
                    .SelectMany(kvp => kvp.Value!.Errors.Select(e => $"{kvp.Key}: {e.ErrorMessage}"))
                    .ToList();

                MembersError = "ModelState invalid:\n" + string.Join("\n", allErrors);

                await LoadPageAsync();
                return Page();
            }

            if (ProjectId == Guid.Empty)
            {
                MembersError = "ProjectId is empty. The form post did not include the {id} route value.";
                await LoadPageAsync();
                return Page();
            }

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            var dto = new ProjectMemberDto
            {
                UserId = AddMemberInput.UserId,
                Role = AddMemberInput.Role
            };

            var resp = await client.PostAsJsonAsync($"/api/projects/{ProjectId}/members", dto);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToPage("/Login");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                MembersError = $"Add member failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}";
                await LoadPageAsync();
                return Page();
            }

            // ✅ Reset the input so it doesn't stay in the textbox
            AddMemberInput = new ProjectMemberDto();

            return Redirect($"/Projects/Details/{ProjectId}?tab=members");
        }


        // ============================================================
        // POST: Remove Member (Owner removes member OR member leaves)
        // ============================================================
        public async Task<IActionResult> OnPostRemoveMemberAsync([FromForm] Guid memberUserId)
        {
            Tab = "members";

            var client = _httpClientFactory.CreateClient("JiraLiteApi");
            var resp = await client.DeleteAsync($"/api/projects/{ProjectId}/members/{memberUserId}");

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToPage("/Login");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                MembersError = $"Remove failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}";
                await LoadPageAsync();
                return Page();
            }

            return Redirect($"/Projects/Details/{ProjectId}?tab=members");
        }

        // ============================================================
        // Page loader
        // ============================================================
        private async Task<IActionResult> LoadPageAsync()
        {
            if (ProjectId == Guid.Empty)
            {
                Error = "Invalid project id.";
                return Page();
            }

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            // 1) Project metadata
            var projectResp = await client.GetAsync("/api/projects");
            if (projectResp.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToPage("/Login");

            if (!projectResp.IsSuccessStatusCode)
            {
                Error = $"API error (projects): {(int)projectResp.StatusCode} {projectResp.ReasonPhrase}";
                return Page();
            }

            var projects = await projectResp.Content.ReadFromJsonAsync<List<ProjectDto>>() ?? new();
            var project = projects.SingleOrDefault(p => p.Id == ProjectId);
            if (project == null)
            {
                Error = "Project not found (or you are not a member).";
                return Page();
            }

            ProjectName = project.Name;
            ProjectDescription = project.Description;

            // 2) Load tab content
            if (string.Equals(Tab, "members", StringComparison.OrdinalIgnoreCase))
            {
                Tab = "members";
                await LoadMembersAsync(client);
            }
            else if (string.Equals(Tab, "activity", StringComparison.OrdinalIgnoreCase))
            {
                Tab = "activity";
                await LoadActivityAsync(client);
            }
            else
            {
                Tab = "board";
                await LoadBoardAsync(client);

                // ✅ always prepare assignee dropdown on board view
                await LoadAssigneeSelectItemsAsync(client);
            }

            return Page();
        }

        private async Task LoadBoardAsync(HttpClient client)
        {
            if (Page < 1) Page = 1;
            if (PageSize < 1) PageSize = 15;

            var url = BuildTasksUrl(ProjectId, Status, Priority, AssigneeId, DueFrom, DueTo, Page, PageSize);

            var tasksResp = await client.GetAsync(url);
            if (!tasksResp.IsSuccessStatusCode)
            {
                var body = await tasksResp.Content.ReadAsStringAsync();
                Error = $"API error (tasks): {(int)tasksResp.StatusCode} {tasksResp.ReasonPhrase}\n{body}";
                return;
            }

            var payload = await tasksResp.Content.ReadFromJsonAsync<PagedResult<TaskItem>>();
            Tasks = payload?.Items?.ToList() ?? new();
            TotalCount = payload?.TotalCount ?? 0;

            TodoTasks = Tasks.Where(t => t.Status == JiraTaskStatus.Todo).ToList();
            InProgressTasks = Tasks.Where(t => t.Status == JiraTaskStatus.InProgress).ToList();
            DoneTasks = Tasks.Where(t => t.Status == JiraTaskStatus.Done).ToList();
        }

        private async Task LoadActivityAsync(HttpClient client)
        {
            if (ActivityPage < 1) ActivityPage = 1;
            if (ActivityPageSize < 1) ActivityPageSize = 50;

            var qs = new List<string>
            {
                $"page={ActivityPage}",
                $"pageSize={ActivityPageSize}"
            };

            if (!string.IsNullOrWhiteSpace(ActivityActionType))
                qs.Add($"actionType={Uri.EscapeDataString(ActivityActionType)}");

            if (ActivityActorId.HasValue)
                qs.Add($"actorId={ActivityActorId.Value}");

            if (ActivityTaskId.HasValue)
                qs.Add($"taskId={ActivityTaskId.Value}");

            if (!string.IsNullOrWhiteSpace(ActivityQ))
                qs.Add($"q={Uri.EscapeDataString(ActivityQ)}");

            var url = $"/api/activity/project/{ProjectId}?{string.Join("&", qs)}";

            var resp = await client.GetAsync(url);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                Error = "Unauthorized.";
                return;
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                Error = $"API error (activity): {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}";
                return;
            }

            ProjectActivity = await resp.Content.ReadFromJsonAsync<PagedResult<ActivityLogDto>>();
        }

        private async Task LoadMembersAsync(HttpClient client)
        {
            var resp = await client.GetAsync($"/api/projects/{ProjectId}/members");

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                MembersError = "Unauthorized.";
                return;
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                MembersError = $"API error (members): {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}";
                return;
            }

            Members = await resp.Content.ReadFromJsonAsync<List<ProjectMemberDto>>() ?? new();

            // ✅ Determine current user + role (used by UI rendering)
            CurrentUserId = GetCurrentUserIdFromJwtCookie();
            CurrentUserRole = Members.FirstOrDefault(m => m.UserId == CurrentUserId)?.Role;
        }

        // ✅ Read current user id from JWT cookie: jiralite_jwt
        private Guid GetCurrentUserIdFromJwtCookie()
        {
            if (!Request.Cookies.TryGetValue("jiralite_jwt", out var token) || string.IsNullOrWhiteSpace(token))
                return Guid.Empty;

            try
            {
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

                // Your API uses NameClaimType = "id"
                var idClaim = jwt.Claims.FirstOrDefault(c => c.Type == "id")?.Value
                           ?? jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                           ?? jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                           ?? jwt.Subject;

                if (string.IsNullOrWhiteSpace(idClaim))
                    return Guid.Empty;

                return Guid.TryParse(idClaim, out var userId) ? userId : Guid.Empty;
            }
            catch
            {
                return Guid.Empty;
            }
        }

        // ✅ This builds your dropdown items for Create Task
        private async Task LoadAssigneeSelectItemsAsync(HttpClient client)
        {
            AssigneeSelectItems = new List<SelectListItem>();

            try
            {
                var resp = await client.GetAsync($"/api/projects/{ProjectId}/members");
                if (!resp.IsSuccessStatusCode) return;

                var members = await resp.Content.ReadFromJsonAsync<List<ProjectMemberDto>>() ?? new();

                // Owner first, then members
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
            catch
            {
                // swallow; dropdown will just be empty
            }
        }

        private static string BuildTasksUrl(
            Guid projectId,
            JiraTaskStatus? status,
            int? priority,
            Guid? assigneeId,
            DateTime? dueFrom,
            DateTime? dueTo,
            int page,
            int pageSize)
        {
            var qs = new List<string>
            {
                $"page={page}",
                $"pageSize={pageSize}"
            };

            if (status.HasValue) qs.Add($"status={status.Value}");
            if (priority.HasValue) qs.Add($"priority={priority.Value}");
            if (assigneeId.HasValue) qs.Add($"assigneeId={assigneeId.Value}");
            if (dueFrom.HasValue) qs.Add($"dueFrom={Uri.EscapeDataString(dueFrom.Value.ToString("yyyy-MM-dd"))}");
            if (dueTo.HasValue) qs.Add($"dueTo={Uri.EscapeDataString(dueTo.Value.ToString("yyyy-MM-dd"))}");

            return $"/api/tasks/project/{projectId}/paged?{string.Join("&", qs)}";
        }

        public string BoardPageLink(int page)
        {
            var parts = new List<string> { "tab=board" };

            if (Status.HasValue) parts.Add($"status={Status.Value}");
            if (Priority.HasValue) parts.Add($"priority={Priority.Value}");
            if (AssigneeId.HasValue) parts.Add($"assigneeId={AssigneeId.Value}");
            if (DueFrom.HasValue) parts.Add($"dueFrom={Uri.EscapeDataString(DueFrom.Value.ToString("yyyy-MM-dd"))}");
            if (DueTo.HasValue) parts.Add($"dueTo={Uri.EscapeDataString(DueTo.Value.ToString("yyyy-MM-dd"))}");

            parts.Add($"page={page}");
            parts.Add($"pageSize={PageSize}");

            return $"/Projects/Details/{ProjectId}?{string.Join("&", parts)}";
        }

        public string ActivityPageLink(int page)
        {
            var parts = new List<string> { "tab=activity" };

            if (!string.IsNullOrWhiteSpace(ActivityActionType)) parts.Add($"actionType={Uri.EscapeDataString(ActivityActionType)}");
            if (ActivityActorId.HasValue) parts.Add($"actorId={ActivityActorId.Value}");
            if (ActivityTaskId.HasValue) parts.Add($"taskId={ActivityTaskId.Value}");
            if (!string.IsNullOrWhiteSpace(ActivityQ)) parts.Add($"q={Uri.EscapeDataString(ActivityQ)}");

            parts.Add($"aPage={page}");
            parts.Add($"aSize={ActivityPageSize}");

            return $"/Projects/Details/{ProjectId}?{string.Join("&", parts)}";
        }

        public string TabLink(string tab)
            => $"/Projects/Details/{ProjectId}?tab={tab}";

        // ✅ RenderColumn (kept exactly as your version)
        public IHtmlContent RenderColumn(string status, List<TaskItem> items)
        {
            var title = status switch
            {
                "InProgress" => "In Progress",
                _ => status
            };

            string PriorityText(int p) => p switch
            {
                1 => "1 (High)",
                2 => "2 (Med-High)",
                3 => "3 (Medium)",
                4 => "4 (Med-Low)",
                5 => "5 (Low)",
                _ => $"{p}"
            };

            string PriorityBadgeClass(int p) => p switch
            {
                1 => "bg-danger",
                2 => "bg-warning text-dark",
                3 => "bg-secondary",
                4 => "bg-info text-dark",
                5 => "bg-light text-dark",
                _ => "bg-secondary"
            };

            (string text, string css) DueBadge(DateTime? due)
            {
                if (!due.HasValue) return ("No due", "bg-light text-dark");

                var d = due.Value.Date;
                var today = DateTime.UtcNow.Date;

                if (d < today) return ($"Overdue ({d:yyyy-MM-dd})", "bg-danger");
                if (d <= today.AddDays(3)) return ($"Due soon ({d:yyyy-MM-dd})", "bg-warning text-dark");

                return ($"{d:yyyy-MM-dd}", "bg-secondary");
            }

            var sb = new StringBuilder();
            sb.AppendLine($@"<div class=""col-md-4"">
  <div class=""border rounded p-2 bg-light board-column"" data-drop-status=""{status}"">
    <div class=""d-flex justify-content-between align-items-center mb-2"">
      <strong>{title}</strong>
      <span class=""badge bg-secondary"">{items.Count}</span>
    </div>");

            if (items.Count == 0)
            {
                sb.AppendLine(@"<div class=""text-muted small"">No items</div>");
            }
            else
            {
                foreach (var t in items)
                {
                    var prioText = PriorityText(t.Priority);
                    var prioCss = PriorityBadgeClass(t.Priority);
                    var (dueText, dueCss) = DueBadge(t.DueDate);

                    var statusSelect = $@"
<select class=""form-select form-select-sm quick-status""
        data-task-id=""{t.Id}"" style=""max-width: 140px;"">
  <option value=""Todo"" {(t.Status == JiraTaskStatus.Todo ? "selected" : "")}>Todo</option>
  <option value=""InProgress"" {(t.Status == JiraTaskStatus.InProgress ? "selected" : "")}>InProgress</option>
  <option value=""Done"" {(t.Status == JiraTaskStatus.Done ? "selected" : "")}>Done</option>
</select>";

                    sb.AppendLine($@"
<a href=""/Tasks/Details/{t.Id}?tab=overview"" class=""text-decoration-none text-dark task-link""
   draggable=""true"" data-task-id=""{t.Id}"" data-status=""{t.Status}"">
  <div class=""card mb-2 shadow-sm"">
    <div class=""card-body p-2"">
      <div class=""d-flex justify-content-between align-items-start gap-2"">
        <div class=""fw-semibold"" style=""line-height: 1.2;"">{System.Net.WebUtility.HtmlEncode(t.Title)}</div>
        <span class=""badge {prioCss}"" title=""Priority"">{prioText}</span>
      </div>");

                    if (!string.IsNullOrWhiteSpace(t.Description))
                    {
                        sb.AppendLine($@"
      <div class=""text-muted small mt-1"" style=""display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;"">
        {System.Net.WebUtility.HtmlEncode(t.Description)}
      </div>");
                    }

                    sb.AppendLine($@"
      <div class=""d-flex justify-content-between align-items-center mt-2"">
        <span class=""badge {dueCss}"" title=""Due date"">{dueText}</span>
        {statusSelect}
      </div>

      <div class=""text-muted small mt-2"">
        Assignee: <code>{t.AssigneeId}</code>
      </div>
    </div>
  </div>
</a>");
                }
            }

            sb.AppendLine(@"  </div>
</div>");

            return new HtmlString(sb.ToString());
        }

        // UI Model (keep here for now; moving to DTO is optional)
        public class CreateTaskInputModel
        {
            [Required]
            [StringLength(100, MinimumLength = 3)]
            public string Title { get; set; } = "";

            [StringLength(2000)]
            public string? Description { get; set; }

            public JiraTaskStatus Status { get; set; } = JiraTaskStatus.Todo;

            [Range(1, 5)]
            public int Priority { get; set; } = 3;

            [Required]
            public Guid AssigneeId { get; set; }

            public DateTime? DueDate { get; set; }
        }
    }
}
