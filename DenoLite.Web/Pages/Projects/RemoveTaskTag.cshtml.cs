using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;

namespace DenoLite.Web.Pages.Projects
{
    [IgnoreAntiforgeryToken]
    public class RemoveTaskTagModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public RemoveTaskTagModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public sealed class RemoveTaskTagRequest
        {
            public Guid TaskId { get; set; }
            public Guid TagId { get; set; }
            public Guid ProjectId { get; set; }
        }

        public async Task<IActionResult> OnPostAsync([FromBody] RemoveTaskTagRequest payload)
        {
            if (payload == null)
                return BadRequest("Payload was null.");
            if (payload.TaskId == Guid.Empty)
                return BadRequest("TaskId is required.");
            if (payload.TagId == Guid.Empty)
                return BadRequest("TagId is required.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");
            var resp = await client.DeleteAsync($"/api/tasks/{payload.TaskId}/tags/{payload.TagId}");

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
