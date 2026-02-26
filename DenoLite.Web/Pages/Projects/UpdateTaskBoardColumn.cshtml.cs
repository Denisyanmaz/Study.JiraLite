using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DenoLite.Web.Pages.Projects
{
    [IgnoreAntiforgeryToken]
    public class UpdateTaskBoardColumnModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public UpdateTaskBoardColumnModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public class UpdateTaskBoardColumnPayload
        {
            [JsonPropertyName("taskId")]
            public Guid TaskId { get; set; }

            [JsonPropertyName("boardColumnId")]
            public Guid BoardColumnId { get; set; }

            [JsonPropertyName("projectId")]
            public Guid ProjectId { get; set; }
        }

        public async Task<IActionResult> OnPostAsync([FromBody] UpdateTaskBoardColumnPayload payload)
        {
            if (payload == null || payload.TaskId == Guid.Empty || payload.BoardColumnId == Guid.Empty)
                return BadRequest("taskId and boardColumnId are required.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");
            var resp = await client.PatchAsJsonAsync(
                $"/api/tasks/{payload.TaskId}/board-column",
                new { boardColumnId = payload.BoardColumnId }
            );

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return StatusCode(401);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, body);
            }
            return new OkResult();
        }
    }
}
