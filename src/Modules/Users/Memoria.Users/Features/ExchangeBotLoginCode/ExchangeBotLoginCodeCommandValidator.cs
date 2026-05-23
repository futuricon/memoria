using FluentValidation;

using Memoria.Users.Contracts.Commands;

namespace Memoria.Users.Features.ExchangeBotLoginCode;

internal sealed class ExchangeBotLoginCodeCommandValidator : AbstractValidator<ExchangeBotLoginCodeCommand>
{
    public ExchangeBotLoginCodeCommandValidator()
    {
        RuleFor(c => c.Code)
            .NotEmpty()
            .Length(6)
            .Matches(@"^\d{6}$").WithMessage("Code must be a 6-digit string.");
    }
}
