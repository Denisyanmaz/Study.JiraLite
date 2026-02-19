using FluentValidation;
using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.Auth;

namespace DenoLite.Application.Validation
{
    public class VerifyEmailDtoValidator : AbstractValidator<VerifyEmailDto>
    {
        public VerifyEmailDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(320);

            RuleFor(x => x.Code)
                .NotEmpty()
                .Length(6)
                .Matches("^[0-9]{6}$")
                .WithMessage("Code must be exactly 6 digits.");
        }
    }
}
