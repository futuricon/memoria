using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Memoria.Users.Contracts.Dtos;

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
    public Role Role { get; }

    public CurrentUser(Guid id, string? email, string displayName, Role role)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        Role = role;
    }

    public bool IsAdmin => Role == Role.Admin;
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

        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;
        var role = Enum.TryParse<Role>(roleClaim, ignoreCase: false, out var parsed)
            ? parsed
            : Role.User;

        return new CurrentUser(
            id,
            user.FindFirst(JwtRegisteredClaimNames.Email)?.Value,
            user.FindFirst(JwtRegisteredClaimNames.Name)?.Value ?? string.Empty,
            role);
    }
}
