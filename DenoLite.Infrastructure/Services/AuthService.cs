using BCrypt.Net;
using DenoLite.Application.DTOs.Auth;
using DenoLite.Application.Interfaces;
using DenoLite.Domain.Entities;
using DenoLite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DenoLite.Application.Exceptions;
using Microsoft.Extensions.Logging;

namespace DenoLite.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly DenoLiteDbContext _db;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AuthService> _logger;

        public AuthService(DenoLiteDbContext db, IConfiguration config, IEmailSender emailSender, ILogger<AuthService> logger)
        {
            _db = db;
            _config = config;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterUserDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (existingUser != null)
            {
                // Check if user registered with Google
                if (!string.IsNullOrEmpty(existingUser.GoogleId))
                {
                    throw new ConflictException("This email is already registered with Google. Please sign in with Google or use the 'Forgot Password' option to set a password.");
                }
                throw new ConflictException("Email already exists");
            }

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
            if (user == null)
                throw new UnauthorizedAccessException("Invalid credentials");

            // ✅ Check if user is Google-only (no password set)
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                throw new UnauthorizedAccessException("This account uses Google sign-in. Please sign in with Google instead.");
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
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

            var existing = await _db.EmailVerifications
                .Where(v => v.UserId == user.Id)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            // ✅ Reset resend count if last send was more than 1 hour ago (so "wait and try again" works)
            var effectiveSendCount = existing == null ? 0
                : (now - existing.LastSentAt).TotalHours >= 1 ? 0
                : existing.SendCount;

            // ✅ cooldown: 60 seconds between sends
            if (existing != null)
            {
                var seconds = (now - existing.LastSentAt).TotalSeconds;
                if (seconds < 60)
                    throw new TooManyRequestsException("Please wait before requesting another code.");
            }

            // ✅ Limit: max 5 resends per hour (effective count resets after 1 hour)
            if (effectiveSendCount >= 5)
                throw new TooManyRequestsException("Too many resend requests. Please try again in an hour.");

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
                SendCount = effectiveSendCount + 1,
                IsUsed = false
            };

            _db.EmailVerifications.Add(record);
            await _db.SaveChangesAsync();

            var subject = "Your DenoLite verification code";
            var html = $@"
                <div style='font-family: Arial, sans-serif;'>
                    <h2>Email Verification</h2>
                    <p>Your verification code is:</p>
                    <div style='font-size: 28px; letter-spacing: 4px; font-weight: bold;'>{code}</div>
                    <p>This code expires in 15 minutes.</p>
                </div>";

            // Fire-and-forget so API returns immediately (avoids Web timeout when SMTP hangs on Render)
            _ = SendVerificationEmailAsync(user.Email, subject, html);
        }

        private async Task SendVerificationEmailAsync(string email, string subject, string html)
        {
            try
            {
                await _emailSender.SendAsync(email, subject, html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to {Email}", email);
            }
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
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

        public async Task ChangePasswordAsync(Guid userId, string oldPassword, string newPassword)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found.");

            // ✅ Check if user is Google-only (no password set)
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                // Google-only users can set a password (to enable email/password login)
                // Skip old password verification
            }
            else
            {
                // Verify old password for users with existing passwords
                if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
                    throw new UnauthorizedAccessException("Current password is incorrect.");
            }

            // Validate new password
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                throw new BadRequestException("New password must be at least 6 characters.");

            if (!string.IsNullOrEmpty(user.PasswordHash) && oldPassword == newPassword)
                throw new BadRequestException("New password must be different from the current password.");

            // Update password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Notify user by email only if they have notifications enabled
            if (!string.IsNullOrWhiteSpace(user.Email) && user.NotificationsEnabled)
                _ = SendPasswordChangedEmailAsync(user.Email);
        }

        public async Task<bool> GetNotificationsEnabledAsync(Guid userId)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.NotificationsEnabled)
                .FirstOrDefaultAsync();
            return user; // default false if not found, but we assume user exists
        }

        public async Task SetNotificationsEnabledAsync(Guid userId, bool enabled)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found.");
            user.NotificationsEnabled = enabled;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        private async Task SendPasswordChangedEmailAsync(string email)
        {
            try
            {
                var subject = "Your DenoLite password was changed";
                var html = @"
                    <div style='font-family: Arial, sans-serif;'>
                        <h2>Password changed</h2>
                        <p>Your DenoLite account password was changed successfully.</p>
                        <p>If you did not make this change, please contact support.</p>
                        <p>— The DenoLite Team</p>
                    </div>";
                await _emailSender.SendAsync(email, subject, html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password-changed notification to {Email}", email);
            }
        }

        public async Task RequestEmailChangeAsync(Guid userId, string password, string newEmail)
        {
            newEmail = newEmail.Trim().ToLowerInvariant();

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found.");

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Password is incorrect.");

            // Check if new email is different
            if (user.Email.ToLower() == newEmail)
                throw new BadRequestException("New email must be different from current email.");

            // Check if new email already exists
            if (await _db.Users.AnyAsync(u => u.Email.ToLower() == newEmail))
                throw new ConflictException("Email already in use.");

            // Create/replace email change verification
            var existingChange = await _db.EmailChangeRequests
                .FirstOrDefaultAsync(r => r.UserId == userId);

            if (existingChange != null)
                _db.EmailChangeRequests.Remove(existingChange);

            var code = GenerateSixDigitCode();
            var hashedCode = HashOtp(newEmail, code);

            var changeRequest = new DenoLite.Domain.Entities.EmailChangeRequest
            {
                UserId = userId,
                NewEmail = newEmail,
                CodeHash = hashedCode,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };

            _db.EmailChangeRequests.Add(changeRequest);
            await _db.SaveChangesAsync();

            // Send OTP to new email
            var otpSubject = "Your Email Change Verification Code";
            var otpBody = $"""
                <p>Hi,</p>
                <p>You requested to change your email address in <strong>DenoLite</strong>.</p>
                <p>Your verification code is: <strong style="font-size: 24px; color: #007bff;">{code}</strong></p>
                <p>This code will expire in 15 minutes.</p>
                <p>If you didn't request this, please ignore this email.</p>
                <p>— The DenoLite Team</p>
                """;

            await _emailSender.SendAsync(newEmail, otpSubject, otpBody);

            // Send warning to old email
            var warningSubject = "Email Change Request Notification";
            var warningBody = $"""
                <p>Hi,</p>
                <p><strong>⚠️ Security Alert</strong></p>
                <p>A request was made to change your email address in <strong>DenoLite</strong> to: <strong>{newEmail}</strong></p>
                <p>If this was you, no action is needed. You will need to verify the new email with a code sent to it.</p>
                <p>If this wasn't you, please secure your account immediately by changing your password.</p>
                <p>— The DenoLite Team</p>
                """;

            try
            {
                await _emailSender.SendAsync(user.Email, warningSubject, warningBody);
            }
            catch
            {
                // Don't fail the operation if warning email fails
            }
        }

        public async Task VerifyAndChangeEmailAsync(Guid userId, string newEmail, string code)
        {
            newEmail = newEmail.Trim().ToLowerInvariant();
            code = code.Trim();

            if (code.Length != 6 || !code.All(char.IsDigit))
                throw new BadRequestException("Invalid code format.");

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found.");

            var changeRequest = await _db.EmailChangeRequests
                .FirstOrDefaultAsync(r => r.UserId == userId && r.NewEmail.ToLower() == newEmail);

            if (changeRequest == null)
                throw new NotFoundException("Email change request not found.");

            if (changeRequest.ExpiresAt < DateTime.UtcNow)
            {
                _db.EmailChangeRequests.Remove(changeRequest);
                await _db.SaveChangesAsync();
                throw new BadRequestException("Code has expired. Please request a new one.");
            }

            var expectedHash = HashOtp(newEmail, code);
            if (!FixedTimeEquals(changeRequest.CodeHash, expectedHash))
                throw new BadRequestException("Invalid verification code.");

            // Update email
            user.Email = newEmail;
            user.IsEmailVerified = true;
            user.UpdatedAt = DateTime.UtcNow;

            _db.EmailChangeRequests.Remove(changeRequest);
            await _db.SaveChangesAsync();
        }

        public async Task<AuthResponseDto> AuthenticateWithGoogleAsync(string googleId, string email)
        {
            email = email.Trim().ToLowerInvariant();

            // Check if user exists by GoogleId or Email (guard against null Email)
            var user = await _db.Users.FirstOrDefaultAsync(u => 
                u.GoogleId == googleId || (u.Email != null && u.Email.ToLower() == email));

            if (user == null)
            {
                // ✅ NEW USER: Create account automatically
                user = new User
                {
                    Email = email,
                    GoogleId = googleId,
                    PasswordHash = string.Empty, // OAuth users don't have passwords
                    Role = "User",
                    IsActive = true,
                    IsEmailVerified = true // Google emails are pre-verified
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }
            else
            {
                // ✅ EXISTING USER: Link Google account if not already linked
                if (string.IsNullOrEmpty(user.GoogleId))
                {
                    user.GoogleId = googleId;
                    await _db.SaveChangesAsync();
                }

                // ✅ EXISTING USER: Ensure email is verified
                if (!user.IsEmailVerified)
                {
                    user.IsEmailVerified = true;
                    await _db.SaveChangesAsync();
                }

                if (!user.IsActive)
                    throw new ForbiddenException("User is inactive.");
            }

            return new AuthResponseDto
            {
                Email = user.Email,
                Role = user.Role,
                Token = GenerateJwtToken(user)
            };
        }

        public async Task RequestPasswordResetAsync(string email)
        {
            email = email.Trim().ToLowerInvariant();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            
            // Security: Don't reveal if user exists or not
            // But if user exists and uses Google-only, guide them
            if (user == null)
            {
                // Silently succeed to prevent email enumeration
                _logger.LogInformation("Password reset requested for non-existent email: {Email}", email);
                return;
            }

            // Check if user is Google-only (no password set)
            if (!string.IsNullOrEmpty(user.GoogleId) && string.IsNullOrEmpty(user.PasswordHash))
            {
                // This is a Google-only user trying to set a password
                _logger.LogInformation("Password reset requested for Google-only user: {Email}", email);
            }

            // Remove any existing password reset requests
            var existingResets = await _db.PasswordResets
                .Where(r => r.UserId == user.Id)
                .ToListAsync();

            if (existingResets.Any())
            {
                _db.PasswordResets.RemoveRange(existingResets);
                await _db.SaveChangesAsync();
            }

            // Generate 6-digit code
            var code = GenerateSixDigitCode();
            var codeHash = HashOtp(email, code);

            var resetRequest = new PasswordReset
            {
                UserId = user.Id,
                CodeHash = codeHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                Attempts = 0,
                IsUsed = false
            };

            _db.PasswordResets.Add(resetRequest);
            await _db.SaveChangesAsync();

            // Send email with reset code
            var subject = "Password Reset Code - DenoLite";
            var isGoogleUser = !string.IsNullOrEmpty(user.GoogleId) && string.IsNullOrEmpty(user.PasswordHash);
            var html = $@"
                <div style='font-family: Arial, sans-serif;'>
                    <h2>Password Reset Request</h2>
                    {(isGoogleUser ? "<p><strong>Note:</strong> You registered with Google. Setting a password will allow you to sign in with either Google or email/password.</p>" : "")}
                    <p>Your password reset code is:</p>
                    <div style='font-size: 28px; letter-spacing: 4px; font-weight: bold; color: #007bff;'>{code}</div>
                    <p>This code expires in 15 minutes.</p>
                    <p>If you didn't request this, please ignore this email.</p>
                    <p>— The DenoLite Team</p>
                </div>";

            // Fire-and-forget
            _ = SendPasswordResetEmailAsync(email, subject, html);
        }

        public async Task ResetPasswordAsync(string email, string code, string newPassword)
        {
            email = email.Trim().ToLowerInvariant();
            code = code.Trim();

            if (code.Length != 6 || !code.All(char.IsDigit))
                throw new BadRequestException("Invalid code format.");

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                throw new BadRequestException("Password must be at least 6 characters.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (user == null)
                throw new NotFoundException("User not found.");

            var resetRequest = await _db.PasswordResets
                .Where(r => r.UserId == user.Id && !r.IsUsed)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (resetRequest == null)
                throw new NotFoundException("No password reset request found. Please request a new code.");

            if (resetRequest.IsUsed)
                throw new ConflictException("This reset code was already used. Please request a new code.");

            if (DateTime.UtcNow > resetRequest.ExpiresAt)
            {
                _db.PasswordResets.Remove(resetRequest);
                await _db.SaveChangesAsync();
                throw new BadRequestException("Reset code expired. Please request a new code.");
            }

            if (resetRequest.Attempts >= 5)
                throw new TooManyRequestsException("Too many attempts. Please request a new code.");

            var actualHash = HashOtp(email, code);

            if (!FixedTimeEquals(resetRequest.CodeHash, actualHash))
            {
                resetRequest.Attempts += 1;
                await _db.SaveChangesAsync();
                throw new BadRequestException("Invalid reset code.");
            }

            // Success - update password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            // Mark reset as used and remove
            _db.PasswordResets.Remove(resetRequest);
            await _db.SaveChangesAsync();

            // Send confirmation email
            _ = SendPasswordResetSuccessEmailAsync(user.Email);
        }

        private async Task SendPasswordResetEmailAsync(string email, string subject, string html)
        {
            try
            {
                await _emailSender.SendAsync(email, subject, html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            }
        }

        private async Task SendPasswordResetSuccessEmailAsync(string email)
        {
            try
            {
                var subject = "Password Reset Successful - DenoLite";
                var html = @"
                    <div style='font-family: Arial, sans-serif;'>
                        <h2>Password Reset Successful</h2>
                        <p>Your DenoLite account password has been reset successfully.</p>
                        <p>You can now sign in with your new password.</p>
                        <p>If you didn't make this change, please contact support immediately.</p>
                        <p>— The DenoLite Team</p>
                    </div>";
                await _emailSender.SendAsync(email, subject, html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset success email to {Email}", email);
            }
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
