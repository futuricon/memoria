using FluentValidation;

using Memoria.Users.Contracts.Commands;

namespace Memoria.Users.Features.GenerateBotLoginCode;

internal sealed class GenerateBotLoginCodeCommandValidator : AbstractValidator<GenerateBotLoginCodeCommand>
{
    public GenerateBotLoginCodeCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
