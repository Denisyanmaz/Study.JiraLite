using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Text.Json.Serialization;

namespace DenoLite.Web.Pages.Projects
{
    [IgnoreAntiforgeryToken]
    public class DeleteBoardColumnModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DeleteBoardColumnModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public class Payload
        {
            [JsonPropertyName("columnId")]
            public Guid ColumnId { get; set; }
        }

        public async Task<IActionResult> OnPostAsync([FromBody] Payload payload)
        {
            if (payload == null || payload.ColumnId == Guid.Empty)
                return BadRequest("columnId is required.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");
            var resp = await client.DeleteAsync($"/api/projects/board-columns/{payload.ColumnId}");
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return StatusCode(401);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, body);
            }
            return new NoContentResult();
        }
    }
}
