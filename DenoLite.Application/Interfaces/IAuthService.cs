using DenoLite.Application.DTOs.Auth;

namespace DenoLite.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterUserDto dto);
        Task<AuthResponseDto> LoginAsync(LoginUserDto dto);
        Task VerifyEmailAsync(string email, string code);
        Task ResendVerificationAsync(string email);
        Task ChangePasswordAsync(Guid userId, string oldPassword, string newPassword);
        Task RequestEmailChangeAsync(Guid userId, string password, string newEmail);
        Task VerifyAndChangeEmailAsync(Guid userId, string newEmail, string code);
    }
}
