using JiraLite.Application.DTOs.Auth;
using JiraLite.Application.Interfaces;
using JiraLite.Infrastructure.Persistence;   // ✅ YOUR DbContext namespace
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JiraLite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly JiraLiteDbContext _db;   // ✅ FIX

        public AuthController(IAuthService authService, JiraLiteDbContext db)
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
    }
}
