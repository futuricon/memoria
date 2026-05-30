using AspNet.Security.OAuth.GitHub;

using Memoria.Api.Configuration;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Api.Authentication;

/// <summary>
/// Cookie + optional Google/GitHub auth schemes for the <c>/jobs</c> dashboard.
/// Isolated from the JWT scheme used by the API: separate cookie name, separate
/// callback paths, separate sign-in scheme.
/// <para>
/// Google and GitHub are registered ONLY when both ClientId and ClientSecret are
/// supplied. Without them <c>OAuthOptions.Validate()</c> would lazily throw on every
/// HTTP request, because the handler is invoked by <c>AuthenticationMiddleware</c>
/// for every scheme that implements <c>IAuthenticationRequestHandler</c> — including
/// unrelated paths like <c>/healthz</c>.
/// </para>
/// </summary>
internal static class OAuthAuthenticationConfiguration
{
    public const string CookieScheme = "HangfireCookie";

    public const string GoogleSpaScheme = "GoogleSpa";
    public const string GitHubSpaScheme = "GitHubSpa";

    public static IServiceCollection AddOAuthAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var oauth = configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>()
            ?? new OAuthOptions();

        var builder = services
            .AddAuthentication()
            .AddCookie(CookieScheme, o =>
            {
                o.LoginPath = "/jobs/login";
                o.AccessDeniedPath = "/jobs/forbidden";
                o.Cookie.Name = "memoria_hangfire";
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.SlidingExpiration = true;
            });

        if (HasCredentials(oauth.Google))
        {
            builder.AddGoogle(GoogleDefaults.AuthenticationScheme, o =>
            {
                o.SignInScheme = CookieScheme;
                o.ClientId = oauth.Google.ClientId;
                o.ClientSecret = oauth.Google.ClientSecret;
                o.CallbackPath = "/jobs/signin-google";
                // Lax survives the cross-site→top-level-GET callback and (unlike the
                // default None) doesn't get dropped when the request scheme looks
                // like http behind the proxy.
                o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
            });
        }

        if (HasCredentials(oauth.GitHub))
        {
            builder.AddGitHub(GitHubAuthenticationDefaults.AuthenticationScheme, o =>
            {
                o.SignInScheme = CookieScheme;
                o.ClientId = oauth.GitHub.ClientId;
                o.ClientSecret = oauth.GitHub.ClientSecret;
                o.CallbackPath = "/jobs/signin-github";
                o.Scope.Add("user:email");
                o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
            });
        }

        // SPA OAuth — short-circuits via OnTicketReceived to issue JWTs and
        // bounce the browser to the SPA callback URL with tokens in the
        // fragment. SignInScheme is set to the Hangfire cookie purely to
        // satisfy AddOAuthHandler's validation; HandleResponse() in the
        // event prevents that scheme from ever being touched.
        if (HasCredentials(oauth.Google))
        {
            builder.AddGoogle(GoogleSpaScheme, o =>
            {
                o.SignInScheme = CookieScheme;
                o.ClientId = oauth.Google.ClientId;
                o.ClientSecret = oauth.Google.ClientSecret;
                o.CallbackPath = "/api/v1/auth/google/callback";
                o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Events.OnTicketReceived = SpaOAuthEvents.HandleGoogleTicket;
            });
        }

        if (HasCredentials(oauth.GitHub))
        {
            builder.AddGitHub(GitHubSpaScheme, o =>
            {
                o.SignInScheme = CookieScheme;
                o.ClientId = oauth.GitHub.ClientId;
                o.ClientSecret = oauth.GitHub.ClientSecret;
                o.CallbackPath = "/api/v1/auth/github/callback";
                o.Scope.Add("user:email");
                o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Events.OnTicketReceived = SpaOAuthEvents.HandleGitHubTicket;
            });
        }

        return services;
    }

    private static bool HasCredentials(OAuthProviderOptions provider) =>
        !string.IsNullOrWhiteSpace(provider.ClientId)
        && !string.IsNullOrWhiteSpace(provider.ClientSecret);
}
