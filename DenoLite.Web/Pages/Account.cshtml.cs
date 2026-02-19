using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Json;

namespace DenoLite.Web.Pages
{
    [IgnoreAntiforgeryToken]
    public class AccountModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult OnGet()
        {
            return NotFound();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            var dto = await Request.ReadFromJsonAsync<ChangePasswordRequest>();
            if (dto == null || string.IsNullOrWhiteSpace(dto.OldPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return BadRequest("Missing password fields.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");

            var response = await client.PostAsJsonAsync("/api/auth/change-password", new
            {
                OldPassword = dto.OldPassword,
                NewPassword = dto.NewPassword
            });

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return Unauthorized();

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, errorText);
            }

            return new JsonResult(new { message = "Password changed successfully." });
        }

        public async Task<IActionResult> OnPostSendEmailChangeCodeAsync()
        {
            var dto = await Request.ReadFromJsonAsync<SendEmailChangeRequest>();
            if (dto == null || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.NewEmail))
                return BadRequest("Missing password or email.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");

            var response = await client.PostAsJsonAsync("/api/auth/request-email-change", new
            {
                Password = dto.Password,
                NewEmail = dto.NewEmail
            });

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return Unauthorized();

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, errorText);
            }

            return new JsonResult(new { message = "Code sent successfully." });
        }

        public async Task<IActionResult> OnPostVerifyAndChangeEmailAsync()
        {
            var dto = await Request.ReadFromJsonAsync<VerifyEmailChangeRequest>();
            if (dto == null || string.IsNullOrWhiteSpace(dto.NewEmail) || string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest("Missing email or code.");

            var client = _httpClientFactory.CreateClient("DenoLiteApi");

            var response = await client.PostAsJsonAsync("/api/auth/verify-email-change", new
            {
                NewEmail = dto.NewEmail,
                Code = dto.Code
            });

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return Unauthorized();

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, errorText);
            }

            return new JsonResult(new { message = "Email changed successfully." });
        }

        public class ChangePasswordRequest
        {
            public string OldPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        public class SendEmailChangeRequest
        {
            public string Password { get; set; } = string.Empty;
            public string NewEmail { get; set; } = string.Empty;
        }

        public class VerifyEmailChangeRequest
        {
            public string NewEmail { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
        }
    }
}
