using FluentValidation;
using DenoLite.Application.DTOs.Auth;

namespace DenoLite.Application.Validation
{
    public class LoginUserDtoValidator : AbstractValidator<LoginUserDto>
    {
        public LoginUserDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(320);

            RuleFor(x => x.Password)
                .NotEmpty()
                .MaximumLength(100);
        }
    }
}
