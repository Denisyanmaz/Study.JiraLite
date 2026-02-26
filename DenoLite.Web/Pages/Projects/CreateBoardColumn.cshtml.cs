using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DenoLite.Web.Pages.Projects
{
    [IgnoreAntiforgeryToken]
    public class CreateBoardColumnModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CreateBoardColumnModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public class Payload
        {
            [JsonPropertyName("projectId")]
            public Guid ProjectId { get; set; }
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";
        }

        public async Task<IActionResult> OnPostAsync([FromBody] Payload payload)
        {
            if (payload == null || payload.ProjectId == Guid.Empty)
                return BadRequest("projectId is required.");
            var name = (payload.Name ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                return BadRequest("name is required.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");
            var resp = await client.PostAsJsonAsync(
                $"/api/projects/{payload.ProjectId}/board-columns",
                new { name = name }
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
