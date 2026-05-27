using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using global::Hangfire.Dashboard;

using Memoria.Api.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Memoria.Api.Hangfire;

/// <summary>
/// Пропускает в Hangfire-dashboard только аутентифицированных через cookie-OAuth
/// пользователей, чей email входит в <see cref="HangfireDashboardOptions.AllowedEmails"/>.
/// При <c>false</c> Hangfire возвращает 401 — middleware из #79 перехватит и
/// перенаправит на <c>/jobs/login</c>.
/// </summary>
internal sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.GetHttpContext();

        if (http.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var email = http.User.FindFirst(ClaimTypes.Email)?.Value
            ?? http.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return false;
        }

        var options = http.RequestServices
            .GetRequiredService<IOptions<HangfireDashboardOptions>>().Value;
        return options.AllowedEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
    }
}
