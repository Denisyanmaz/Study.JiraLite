using DenoLite.Application.DTOs.Auth;
using DenoLite.Application.Interfaces;
using DenoLite.Infrastructure.Persistence;   // ✅ YOUR DbContext namespace
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace DenoLite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly DenoLiteDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, DenoLiteDbContext db, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _authService = authService;
            _db = db;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto dto)
        {
            var result = await _authService.RegisterAsync(dto);
            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            return Ok(result);
        }
        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
        {
            await _authService.VerifyEmailAsync(dto.Email, dto.Code);
            return Ok(new { message = "Email verified." });
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto)
        {
            await _authService.ResendVerificationAsync(dto.Email);
            return Ok(new { message = "Verification code sent." });
        }


        [Authorize]
        [HttpGet("/api/users/resolve")]
        public async Task<IActionResult> ResolveUser([FromQuery] string q)
        {
            if (Guid.TryParse(q, out var id))
                return Ok(new { userId = id });

            var email = q.Trim().ToLower();

            var user = await _db.Users
                .Where(u => u.Email.ToLower() == email)
                .Select(u => new { u.Id })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound("User not found.");

            return Ok(new { userId = user.Id });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("Invalid user token.");

            await _authService.ChangePasswordAsync(userId, dto.OldPassword, dto.NewPassword);
            return Ok(new { message = "Password changed successfully." });
        }

        [Authorize]
        [HttpPost("request-email-change")]
        public async Task<IActionResult> RequestEmailChange([FromBody] RequestEmailChangeDto dto)
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("Invalid user token.");

            await _authService.RequestEmailChangeAsync(userId, dto.Password, dto.NewEmail);
            return Ok(new { message = "Verification code sent." });
        }

        [Authorize]
        [HttpPost("verify-email-change")]
        public async Task<IActionResult> VerifyEmailChange([FromBody] VerifyEmailChangeDto dto)
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("Invalid user token.");

            await _authService.VerifyAndChangeEmailAsync(userId, dto.NewEmail, dto.Code);
            return Ok(new { message = "Email changed successfully." });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("Invalid user token.");

            var notificationsEnabled = await _authService.GetNotificationsEnabledAsync(userId);
            return Ok(new { notificationsEnabled });
        }

        [Authorize]
        [HttpPatch("notifications")]
        public async Task<IActionResult> UpdateNotifications([FromBody] UpdateNotificationsDto dto)
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("Invalid user token.");

            await _authService.SetNotificationsEnabledAsync(userId, dto.Enabled);
            return Ok(new { notificationsEnabled = dto.Enabled });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            await _authService.RequestPasswordResetAsync(dto.Email);
            return Ok(new { message = "If an account exists with this email, a password reset code has been sent." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            await _authService.ResetPasswordAsync(dto.Email, dto.Code, dto.NewPassword);
            return Ok(new { message = "Password reset successfully. You can now login with your new password." });
        }

        [HttpGet("google-login")]
        public IActionResult GoogleLogin()
        {
            // Redirect to a handler endpoint after Google authentication completes
            var redirectUrl = Url.Action(nameof(GoogleCallbackHandler), "Auth", null, Request.Scheme);
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, "Google");
        }

        [HttpGet("google-callback-handler")]
        public async Task<IActionResult> GoogleCallbackHandler()
        {
            _logger.LogInformation("=== Google callback handler endpoint hit ===");
            try
            {
                _logger.LogInformation("Google callback handler received");

                // Google signs into Cookies, so read from Cookie scheme
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                _logger.LogInformation($"Authentication result: Succeeded={result.Succeeded}, Principal={result.Principal != null}");

                if (!result.Succeeded || result.Principal == null)
                {
                    _logger.LogWarning("Google authentication failed or principal is null");
                    var webAppUrl = GetWebAppUrl();
                    return Redirect($"{webAppUrl}/GoogleCallback?error=auth_failed");
                }

                // Extract claims from the authenticated principal
                var claims = result.Principal.Claims.ToList();
                _logger.LogInformation($"Found {claims.Count} claims");
                
                var googleId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

                _logger.LogInformation($"GoogleId: {googleId}, Email: {email}");

                if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("Missing GoogleId or Email in claims");
                    var webAppUrl = GetWebAppUrl();
                    return Redirect($"{webAppUrl}/GoogleCallback?error=missing_info");
                }

                // Authenticate user in our system
                _logger.LogInformation("Calling AuthenticateWithGoogleAsync");
                var authResult = await _authService.AuthenticateWithGoogleAsync(googleId, email);

                // Sign out from Cookie authentication (cleanup)
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Redirect to Web app callback page with token
                var baseUrl = GetWebAppUrl();
                var token = Uri.EscapeDataString(authResult.Token);
                _logger.LogInformation("Redirecting to Web app with token");
                return Redirect($"{baseUrl}/GoogleCallback?token={token}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Google callback: {Message}", ex.Message);
                var webAppUrl = GetWebAppUrl();
                return Redirect($"{webAppUrl}/GoogleCallback?error=server_error");
            }
        }

        private string GetWebAppUrl()
        {
            // Try configuration first
            var configuredUrl = _configuration["WebApp:BaseUrl"];
            if (!string.IsNullOrEmpty(configuredUrl))
                return configuredUrl;

            // Fallback: construct from current request
            var scheme = Request.Scheme;
            var host = Request.Host.Host;
            var port = Request.Host.Port;
            
            // In development, Web app typically runs on port 5001 (HTTPS) or 5000 (HTTP)
            // API typically runs on different port
            // For now, use same host but assume Web app is on standard ports
            if (port.HasValue && port.Value != 5001 && port.Value != 5000)
            {
                // API is on different port, assume Web app is on 5001
                return $"{scheme}://{host}:5001";
            }
            
            return $"{scheme}://{host}" + (port.HasValue ? $":{port.Value}" : "");
        }
    }
}
