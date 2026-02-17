using BCrypt.Net;
using JiraLite.Application.DTOs.Auth;
using JiraLite.Application.Interfaces;
using JiraLite.Domain.Entities;
using JiraLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JiraLite.Application.Exceptions;


namespace JiraLite.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly JiraLiteDbContext _db;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;

        public AuthService(JiraLiteDbContext db, IConfiguration config, IEmailSender emailSender)
        {
            _db = db;
            _config = config;
            _emailSender = emailSender;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterUserDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            if (await _db.Users.AnyAsync(u => u.Email.ToLower() == email))
                throw new ConflictException("Email already exists");

            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "User",
                IsActive = true,
                IsEmailVerified = false
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // ✅ Create verification + send code (OTP)
            await CreateOrReplaceVerificationAndSendAsync(user);

            // You can return token to keep old behavior,
            // but login will still be blocked until verified.
            return new AuthResponseDto
            {
                Email = user.Email,
                Role = user.Role,
                Token = GenerateJwtToken(user)
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginUserDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials");

            if (!user.IsActive)
                throw new ForbiddenException("User is inactive.");

            // ✅ Strict policy: no login until email verified
            if (!user.IsEmailVerified)
                throw new ForbiddenException("Email not verified. Please verify your email first.");

            return new AuthResponseDto
            {
                Email = user.Email,
                Role = user.Role,
                Token = GenerateJwtToken(user)
            };
        }

        // ✅ NEW: Verify OTP and mark email verified
        public async Task VerifyEmailAsync(string email, string code)
        {
            email = email.Trim().ToLowerInvariant();
            code = code.Trim();

            if (code.Length != 6 || !code.All(char.IsDigit))
                throw new BadRequestException("Invalid code format.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (user == null)
                throw new NotFoundException("User not found.");

            if (user.IsEmailVerified)
                return; // already verified => success

            var record = await _db.EmailVerifications.SingleOrDefaultAsync(v => v.UserId == user.Id);
            if (record == null)
                throw new NotFoundException("No verification request found. Please resend code.");

            if (record.IsUsed)
                throw new ConflictException("This verification code was already used. Please resend code.");

            if (DateTime.UtcNow > record.ExpiresAt)
                throw new BadRequestException("Verification code expired. Please resend code.");

            if (record.Attempts >= 5)
                throw new TooManyRequestsException("Too many attempts. Please resend code.");

            var actualHash = HashOtp(email, code);

            if (!FixedTimeEquals(record.CodeHash, actualHash))
            {
                record.Attempts += 1;
                await _db.SaveChangesAsync();
                throw new BadRequestException("Invalid verification code.");
            }

            // ✅ Success
            user.IsEmailVerified = true;

            // Either delete or mark used. Deleting keeps table clean.
            _db.EmailVerifications.Remove(record);

            await _db.SaveChangesAsync();
        }

        // ✅ NEW: resend verification code (rate-limited)
        public async Task ResendVerificationAsync(string email)
        {
            email = email.Trim().ToLowerInvariant();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (user == null)
                throw new NotFoundException("User not found.");

            if (user.IsEmailVerified)
                return;

            await CreateOrReplaceVerificationAndSendAsync(user);
        }

        private async Task CreateOrReplaceVerificationAndSendAsync(User user)
        {
            var now = DateTime.UtcNow;

            var existing = await _db.EmailVerifications.SingleOrDefaultAsync(v => v.UserId == user.Id);
            var previousSendCount = existing?.SendCount ?? 0;

            // ✅ cooldown: 60 seconds between sends
            if (existing != null)
            {
                var seconds = (now - existing.LastSentAt).TotalSeconds;
                if (seconds < 60)
                    throw new TooManyRequestsException("Please wait before requesting another code.");
            }

            // ✅ basic resend limit
            if (previousSendCount >= 5)
                throw new Exception("Too many resend requests. Please try later.");

            // replace old record if exists
            if (existing != null)
            {
                _db.EmailVerifications.Remove(existing);
                await _db.SaveChangesAsync();
            }

            var code = GenerateSixDigitCode();
            var codeHash = HashOtp(user.Email, code);

            var record = new EmailVerification
            {
                UserId = user.Id,
                CodeHash = codeHash,
                ExpiresAt = now.AddMinutes(15),
                Attempts = 0,
                LastSentAt = now,
                SendCount = previousSendCount + 1,
                IsUsed = false
            };

            _db.EmailVerifications.Add(record);
            await _db.SaveChangesAsync();

            var subject = "Your JiraLite verification code";
            var html = $@"
                <div style='font-family: Arial, sans-serif;'>
                    <h2>Email Verification</h2>
                    <p>Your verification code is:</p>
                    <div style='font-size: 28px; letter-spacing: 4px; font-weight: bold;'>{code}</div>
                    <p>This code expires in 15 minutes.</p>
                </div>";

            await _emailSender.SendAsync(user.Email, subject, html);
        }

        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim("id", user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("email_verified", user.IsEmailVerified ? "true" : "false")
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(4),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateSixDigitCode()
        {
            var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return n.ToString("D6");
        }

        private string HashOtp(string email, string code)
        {
            var secret = _config["Otp:Secret"];
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("Missing Otp:Secret configuration.");

            var payload = $"{email}:{code}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(bytes);
        }

        private bool FixedTimeEquals(string a, string b)
        {
            var ba = Encoding.UTF8.GetBytes(a);
            var bb = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(ba, bb);
        }
    }
}
