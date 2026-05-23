using FluentValidation;

using Memoria.Cards.Contracts.Commands;

namespace Memoria.Cards.Features.PermanentlyDeleteCard;

internal sealed class PermanentlyDeleteCardCommandValidator : AbstractValidator<PermanentlyDeleteCardCommand>
{
    public PermanentlyDeleteCardCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CardId).NotEmpty();
    }
}
