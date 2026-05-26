using System.IdentityModel.Tokens.Jwt;

using Microsoft.AspNetCore.Http;

namespace Memoria.Api.Authentication;

/// <summary>
/// Контекст текущего пользователя, извлечённый из JWT-токена. Endpoint-ы
/// используют <see cref="HttpContextExtensions.GetCurrentUser"/> вместо
/// прямого <c>HttpContext.User.Claims</c>.
/// </summary>
public sealed class CurrentUser
{
    public Guid Id { get; }
    public string? Email { get; }
    public string DisplayName { get; }

    public CurrentUser(Guid id, string? email, string displayName)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
    }
}

public static class HttpContextExtensions
{
    public static CurrentUser GetCurrentUser(this HttpContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var user = ctx.User ?? throw new InvalidOperationException("HttpContext.User is null");

        var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? throw new UnauthorizedAccessException("Missing 'sub' claim.");

        if (!Guid.TryParse(sub, out var id))
        {
            throw new UnauthorizedAccessException("Invalid 'sub' claim — expected a Guid.");
        }

        return new CurrentUser(
            id,
            user.FindFirst(JwtRegisteredClaimNames.Email)?.Value,
            user.FindFirst(JwtRegisteredClaimNames.Name)?.Value ?? string.Empty);
    }
}
