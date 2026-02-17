using JiraLite.Application.DTOs.Project;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Json;

namespace JiraLite.Web.Pages.Projects
{
    public class CreateModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CreateModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [BindProperty]
        public CreateProjectDto Input { get; set; } = new();

        public string? Error { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            var resp = await client.PostAsJsonAsync("/api/projects", Input);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToPage("/Login");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                Error = $"API error: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}";
                return Page();
            }

            return RedirectToPage("/Projects/Index");
        }
    }
}
