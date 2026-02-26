using DenoLite.Application.DTOs.Task;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Json;

namespace DenoLite.Web.Pages.Projects
{
    [IgnoreAntiforgeryToken]
    public class AddTaskTagModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AddTaskTagModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public sealed class AddTaskTagRequest
        {
            public Guid TaskId { get; set; }
            public Guid ProjectId { get; set; }
            public string Label { get; set; } = "";
            public string Color { get; set; } = "#6c757d";
        }

        public async Task<IActionResult> OnPostAsync([FromBody] AddTaskTagRequest payload)
        {
            if (payload == null)
                return BadRequest("Payload was null.");
            if (payload.TaskId == Guid.Empty)
                return BadRequest("TaskId is required.");
            if (string.IsNullOrWhiteSpace(payload.Label))
                return BadRequest("Label is required.");
            if (payload.Label.Length > 20)
                payload.Label = payload.Label.Substring(0, 20);

            var client = _httpClientFactory.CreateClient("DenoLiteApi");
            var resp = await client.PostAsJsonAsync(
                $"/api/tasks/{payload.TaskId}/tags",
                new { label = payload.Label.Trim(), color = payload.Color ?? "#6c757d" }
            );

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return StatusCode(401);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, body);
            }

            var tag = await resp.Content.ReadFromJsonAsync<TaskTagDto>();
            return new JsonResult(tag);
        }
    }
}
