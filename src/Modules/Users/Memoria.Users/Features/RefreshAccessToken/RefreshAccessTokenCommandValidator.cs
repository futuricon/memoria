using FluentValidation;

using Memoria.Users.Contracts.Commands;

namespace Memoria.Users.Features.RefreshAccessToken;

internal sealed class RefreshAccessTokenCommandValidator : AbstractValidator<RefreshAccessTokenCommand>
{
    public RefreshAccessTokenCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty().MinimumLength(20);
    }
}
