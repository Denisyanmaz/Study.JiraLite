using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DenoLite.Web.Pages.Projects
{
    [IgnoreAntiforgeryToken]
    public class UpdateBoardColumnModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public UpdateBoardColumnModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public class Payload
        {
            [JsonPropertyName("columnId")]
            public Guid ColumnId { get; set; }
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";
        }

        public async Task<IActionResult> OnPostAsync([FromBody] Payload payload)
        {
            if (payload == null || payload.ColumnId == Guid.Empty)
                return BadRequest("columnId is required.");
            var name = (payload.Name ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                return BadRequest("name is required.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");
            var resp = await client.PatchAsJsonAsync(
                $"/api/projects/board-columns/{payload.ColumnId}",
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
