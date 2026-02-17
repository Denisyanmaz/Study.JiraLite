using JiraLite.Application.DTOs.Auth;
using JiraLite.Web.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Security.Claims;

namespace JiraLite.Web.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public LoginModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [BindProperty]
        public LoginInput Input { get; set; } = new();
        public string? Success { get; set; }
        public string? Error { get; set; }

        public void OnGet(string? email = null, bool verified = false)
        {
            if (!string.IsNullOrWhiteSpace(email))
                Input.Email = email;

            if (verified)
                Success = "Email verified successfully. You can now log in.";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            // ✅ typed request matching your API DTO
            var payload = new LoginUserDto
            {
                Email = Input.Email,
                Password = Input.Password
            };

            var resp = await client.PostAsJsonAsync("/api/auth/login", payload);

            if (!resp.IsSuccessStatusCode)
            {
                var msg = await ApiErrorReader.ReadFriendlyMessageAsync(resp);

                if ((int)resp.StatusCode == 403 &&
                    msg.Contains("not verified", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToPage("/VerifyEmail", new { email = Input.Email });
                }

                Error = msg;
                return Page();
            }

            var auth = await resp.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (auth == null || string.IsNullOrWhiteSpace(auth.Token))
            {
                Error = "Login failed: invalid response from server.";
                return Page();
            }

            // ✅ Store JWT in HttpOnly cookie
            Response.Cookies.Append(
                "jiralite_jwt",
                auth.Token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(4) // matches API token lifetime
                });

            // Optional: not sensitive, for UI display
            Response.Cookies.Append("jiralite_email", auth.Email, new CookieOptions
            {
                HttpOnly = false,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(4)
            });
            // ✅ Also sign-in using ASP.NET Cookie auth so User.Identity.IsAuthenticated becomes true
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, auth.Email ?? Input.Email),
            };

            var identity = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(4)
            });


            return RedirectToPage("/Projects/Index");
        }

        public class LoginInput
        {
            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; } = "";

            // ✅ match API rules (min 6)
            [Required, StringLength(100, MinimumLength = 6)]
            public string Password { get; set; } = "";
        }
    }
}
