using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// Generic OAuth sign-in for SPA flows. <paramref name="Provider"/> is the
/// canonical provider name (<c>"Google"</c> / <c>"GitHub"</c>) — the handler
/// parses it to the internal enum so this contract stays serializable.
/// </summary>
/// <param name="Provider">Provider name; must match an <c>IdentityProvider</c> value.</param>
/// <param name="ExternalId">Provider-side subject id (Google <c>sub</c>, GitHub numeric id).</param>
/// <param name="Email">Email asserted by the provider, if any. Used for cross-provider linking when verified.</param>
/// <param name="EmailVerified">Whether the provider has verified ownership of <paramref name="Email"/>.</param>
/// <param name="DisplayName">Human-readable name to seed a freshly created user.</param>
public sealed record AuthenticateOAuthCommand(
    string Provider,
    string ExternalId,
    string? Email,
    bool EmailVerified,
    string DisplayName) : IRequest<Result<JwtTokenPairDto>>;
