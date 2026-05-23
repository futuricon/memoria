using FluentValidation;

using Memoria.Cards.Contracts.Commands;

namespace Memoria.Cards.Features.AddCard;

internal sealed class AddCardCommandValidator : AbstractValidator<AddCardCommand>
{
    public AddCardCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(4000);
        RuleFor(c => c.Tags).NotNull();
    }
}
