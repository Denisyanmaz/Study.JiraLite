using JiraLite.Application.DTOs.Auth;

namespace JiraLite.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterUserDto dto);
        Task<AuthResponseDto> LoginAsync(LoginUserDto dto);
        Task VerifyEmailAsync(string email, string code);
        Task ResendVerificationAsync(string email);

    }
}
