using FluentValidation;

using Memoria.Users.Contracts.Commands;

namespace Memoria.Users.Features.CompleteTelegramLinking;

internal sealed class CompleteTelegramLinkingCommandValidator : AbstractValidator<CompleteTelegramLinkingCommand>
{
    public CompleteTelegramLinkingCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(64);
        RuleFor(c => c.TelegramId).NotEmpty().MaximumLength(64);
    }
}
