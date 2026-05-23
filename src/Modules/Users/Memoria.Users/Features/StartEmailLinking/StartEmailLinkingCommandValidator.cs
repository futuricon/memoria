using FluentValidation;

using Memoria.Users.Contracts.Commands;

namespace Memoria.Users.Features.StartEmailLinking;

internal sealed class StartEmailLinkingCommandValidator : AbstractValidator<StartEmailLinkingCommand>
{
    public StartEmailLinkingCommandValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty()
            .MaximumLength(320)
            .EmailAddress();
    }
}
