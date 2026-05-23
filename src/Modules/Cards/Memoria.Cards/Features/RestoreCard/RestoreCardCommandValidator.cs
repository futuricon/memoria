using FluentValidation;

using Memoria.Cards.Contracts.Commands;

namespace Memoria.Cards.Features.RestoreCard;

internal sealed class RestoreCardCommandValidator : AbstractValidator<RestoreCardCommand>
{
    public RestoreCardCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CardId).NotEmpty();
    }
}
