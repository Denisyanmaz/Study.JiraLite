using JiraLite.Application.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;

namespace JiraLite.Web.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public RegisterModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [BindProperty]
        public RegisterInput Input { get; set; } = new();

        public string? Error { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            // 1) Register
            var registerPayload = new RegisterUserDto
            {
                Email = Input.Email,
                Password = Input.Password
            };

            var registerResp = await client.PostAsJsonAsync("/api/auth/register", registerPayload);

            if (!registerResp.IsSuccessStatusCode)
            {
                var body = await registerResp.Content.ReadAsStringAsync();
                Error = $"Register failed: {(int)registerResp.StatusCode} {registerResp.ReasonPhrase}\n{body}";
                return Page();
            }
            // ✅ After register, go to verification page
            return RedirectToPage("/VerifyEmail", new { email = Input.Email });

        }

        public class RegisterInput
        {
            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; } = "";

            [Required, StringLength(100, MinimumLength = 6)]
            public string Password { get; set; } = "";

            [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; } = "";
        }
    }
}
