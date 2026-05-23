using FluentValidation;

using Memoria.Cards.Contracts.Commands;

namespace Memoria.Cards.Features.SoftDeleteCard;

internal sealed class SoftDeleteCardCommandValidator : AbstractValidator<SoftDeleteCardCommand>
{
    public SoftDeleteCardCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CardId).NotEmpty();
    }
}
