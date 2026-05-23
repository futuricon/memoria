using FluentValidation;

using Memoria.Users.Contracts.Commands;

namespace Memoria.Users.Features.CompleteEmailLinking;

internal sealed class CompleteEmailLinkingCommandValidator : AbstractValidator<CompleteEmailLinkingCommand>
{
    public CompleteEmailLinkingCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(c => c.Code).NotEmpty().Length(6).Matches(@"^\d{6}$");
    }
}
