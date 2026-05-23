using FluentValidation;

using Memoria.Cards.Contracts.Commands;

namespace Memoria.Cards.Features.UpdateCard;

internal sealed class UpdateCardCommandValidator : AbstractValidator<UpdateCardCommand>
{
    public UpdateCardCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CardId).NotEmpty();
        RuleFor(c => c.Title).MaximumLength(200).When(c => c.Title is not null);
        RuleFor(c => c.Body).MaximumLength(4000).When(c => c.Body is not null);
    }
}
