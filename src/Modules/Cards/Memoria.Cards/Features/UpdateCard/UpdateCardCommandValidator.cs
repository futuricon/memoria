using FluentValidation;

using Memoria.Cards.Contracts;
using Memoria.Cards.Contracts.Commands;

namespace Memoria.Cards.Features.UpdateCard;

internal sealed class UpdateCardCommandValidator : AbstractValidator<UpdateCardCommand>
{
    public UpdateCardCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CardId).NotEmpty();
        RuleFor(c => c.Title).MaximumLength(CardConstraints.MaxTitleLength).When(c => c.Title is not null);
        RuleFor(c => c.Body).MaximumLength(CardConstraints.MaxBodyLength).When(c => c.Body is not null);
    }
}
