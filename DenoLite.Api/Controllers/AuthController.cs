using DenoLite.Application.DTOs.Auth;
using DenoLite.Application.Interfaces;
using DenoLite.Infrastructure.Persistence;   // ✅ YOUR DbContext namespace
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DenoLite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly DenoLiteDbContext _db;   // ✅ FIX

        public AuthController(IAuthService authService, DenoLiteDbContext db)
        {
            _authService = authService;
            _db = db;
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
    }
}
