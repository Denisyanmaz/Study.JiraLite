using DenoLite.Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DenoLite.Tests
{
    public static class TestHelpers
    {
        // ? MUST match Program.cs (Issuer/Audience/Key)
        private const string Issuer = "DenoLite.Api";
        private const string Audience = "DenoLite.Api";
        private const string Key = "ThisIsASuperSecretKeyForJWT1234567890!";

        public static User CreateUser(string email)
        {
            return new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = "test",
                Role = "User",
                IsActive = true
            };
        }

        public static string GenerateJwt(User user, TimeSpan? lifetime = null)
        {
            var id = user.Id.ToString();

            // include several common id claim keys to avoid mapping differences
            var claims = new[]
            {
                new Claim("id", id),
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim("nameid", id),
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static string GenerateJwtWithWrongKey(User user)
        {
            var id = user.Id.ToString();

            var claims = new[]
            {
                new Claim("id", id),
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim("nameid", id),
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
            };

            var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("WRONG_WRONG_WRONG_WRONG_WRONG_1234567890!!"));
            var creds = new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
