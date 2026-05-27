using AspNet.Security.OAuth.GitHub;

using Memoria.Api.Configuration;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Api.Authentication;

/// <summary>
/// Cookie + Google + GitHub auth-схемы для <c>/jobs</c> dashboard. Никакого
/// пересечения с JWT-схемой, используемой API: разные cookie name, разные
/// callback paths, разный sign-in scheme.
/// </summary>
internal static class OAuthAuthenticationConfiguration
{
    public const string CookieScheme = "HangfireCookie";

    public static IServiceCollection AddOAuthAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var oauth = configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>()
            ?? throw new InvalidOperationException("OAuth configuration is missing.");

        services
            .AddAuthentication()
            .AddCookie(CookieScheme, o =>
            {
                o.LoginPath = "/jobs/login";
                o.AccessDeniedPath = "/jobs/forbidden";
                o.Cookie.Name = "memoria_hangfire";
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.SlidingExpiration = true;
            })
            .AddGoogle(GoogleDefaults.AuthenticationScheme, o =>
            {
                o.SignInScheme = CookieScheme;
                o.ClientId = oauth.Google.ClientId;
                o.ClientSecret = oauth.Google.ClientSecret;
                o.CallbackPath = "/jobs/signin-google";
            })
            .AddGitHub(GitHubAuthenticationDefaults.AuthenticationScheme, o =>
            {
                o.SignInScheme = CookieScheme;
                o.ClientId = oauth.GitHub.ClientId;
                o.ClientSecret = oauth.GitHub.ClientSecret;
                o.CallbackPath = "/jobs/signin-github";
                o.Scope.Add("user:email");
            });

        return services;
    }
}
