using FluentValidation;
using DenoLite.Application.DTOs;
using DenoLite.Application.DTOs.Auth;

namespace DenoLite.Application.Validation
{
    public class ResendVerificationDtoValidator : AbstractValidator<ResendVerificationDto>
    {
        public ResendVerificationDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(320);
        }
    }
}
