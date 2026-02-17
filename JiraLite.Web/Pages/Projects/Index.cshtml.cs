using JiraLite.Application.DTOs.Project;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Json;

namespace JiraLite.Web.Pages.Projects
{
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public List<ProjectDto> Projects { get; private set; } = new();
        public string? Error { get; private set; }

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task OnGet()
        {
            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            try
            {
                var resp = await client.GetAsync("/api/projects");

                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Error = "You are not logged in. Please login first.";
                    return;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    Error = $"API error: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}";
                    return;
                }

                var projects = await resp.Content.ReadFromJsonAsync<List<ProjectDto>>();
                Projects = projects ?? new List<ProjectDto>();
            }
            catch (Exception ex)
            {
                Error = $"Error calling API: {ex.Message}";
            }
        }
    }
}
