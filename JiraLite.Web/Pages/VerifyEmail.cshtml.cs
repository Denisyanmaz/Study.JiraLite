using JiraLite.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;

namespace JiraLite.Web.Pages
{
    public class VerifyEmailModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public VerifyEmailModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [BindProperty]
        public VerifyEmailInput Input { get; set; } = new();

        public string? Error { get; set; }
        public string? Success { get; set; }

        public void OnGet(string? email = null)
        {
            if (!string.IsNullOrWhiteSpace(email))
                Input.Email = email;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            // API expects: { email, code }
            var resp = await client.PostAsJsonAsync("/api/auth/verify-email", new
            {
                email = Input.Email,
                code = Input.Code
            });

            if (!resp.IsSuccessStatusCode)
            {
                Error = await ApiErrorReader.ReadFriendlyMessageAsync(resp);
                return Page();
            }

            // Optional: redirect to login with email prefilled
            return RedirectToPage("/Login", new { email = Input.Email, verified = true });
        }

        public async Task<IActionResult> OnPostResendAsync()
        {
            // ✅ Resend doesn't need code. Remove Code validation errors if they exist.
            ModelState.Remove("Input.Code");
            Input.Code = ""; // optional: clear the textbox

            // Also clear any previous errors so they don't show under the field
            ModelState.Clear();
            // Validate email only for resend
            if (string.IsNullOrWhiteSpace(Input.Email))
            {
                Error = "Please enter your email to resend the code.";
                return Page();
            }

            var client = _httpClientFactory.CreateClient("JiraLiteApi");

            var resp = await client.PostAsJsonAsync("/api/auth/resend-verification", new
            {
                email = Input.Email
            });

            if (!resp.IsSuccessStatusCode)
            {
                Error = await ApiErrorReader.ReadFriendlyMessageAsync(resp);
                return Page();
            }

            Success = "A new verification code was sent. Please check your email.";
            return Page();
        }

        public class VerifyEmailInput
        {
            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; } = "";

            [Required, StringLength(6, MinimumLength = 6)]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be 6 digits.")]
            public string Code { get; set; } = "";
        }
    }
}
