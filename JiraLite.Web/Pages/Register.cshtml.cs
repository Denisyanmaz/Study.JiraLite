using JiraLite.Application.DTOs;
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

            // 2) Auto-login after register (since register result shape may vary)
            var loginPayload = new LoginUserDto
            {
                Email = Input.Email,
                Password = Input.Password
            };

            var loginResp = await client.PostAsJsonAsync("/api/auth/login", loginPayload);

            if (!loginResp.IsSuccessStatusCode)
            {
                // Register ok but login failed => send them to login screen
                return RedirectToPage("/Login");
            }

            var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (auth == null || string.IsNullOrWhiteSpace(auth.Token))
            {
                return RedirectToPage("/Login");
            }

            // Store JWT cookie (same style as Login)
            Response.Cookies.Append(
                "jiralite_jwt",
                auth.Token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(4)
                });

            Response.Cookies.Append("jiralite_email", auth.Email, new CookieOptions
            {
                HttpOnly = false,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(4)
            });

            return RedirectToPage("/Projects/Index");
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
