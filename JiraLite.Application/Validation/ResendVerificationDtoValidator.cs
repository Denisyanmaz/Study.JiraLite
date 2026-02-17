using FluentValidation;
using JiraLite.Application.DTOs;
using JiraLite.Application.DTOs.Auth;

namespace JiraLite.Api.Validation
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
