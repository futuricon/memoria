using FluentValidation;

using Memoria.Cards.Contracts;
using Memoria.Cards.Contracts.Commands;

namespace Memoria.Cards.Features.AddCard;

internal sealed class AddCardCommandValidator : AbstractValidator<AddCardCommand>
{
    public AddCardCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(CardConstraints.MaxTitleLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(CardConstraints.MaxBodyLength);
        RuleFor(c => c.Tags).NotNull();
    }
}
