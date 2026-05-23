using FluentValidation;

using Memoria.Users.Contracts.Commands;

namespace Memoria.Users.Features.StartTelegramLinking;

internal sealed class StartTelegramLinkingCommandValidator : AbstractValidator<StartTelegramLinkingCommand>
{
    public StartTelegramLinkingCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
