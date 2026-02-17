using FluentValidation;
using JiraLite.Application.DTOs.Auth;

namespace JiraLite.Api.Validation
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
