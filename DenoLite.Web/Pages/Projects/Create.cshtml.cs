using DenoLite.Application.DTOs.Project;
using DenoLite.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Json;

namespace DenoLite.Web.Pages.Projects
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

            var client = _httpClientFactory.CreateClient("DenoLiteApi");

            var resp = await client.PostAsJsonAsync("/api/projects", Input);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return RedirectToPage("/Login");

            if (!resp.IsSuccessStatusCode)
            {
                Error = await ApiErrorReader.ReadFriendlyMessageAsync(resp);
                return Page();
            }

            return RedirectToPage("/Projects/Index");
        }
    }
}
