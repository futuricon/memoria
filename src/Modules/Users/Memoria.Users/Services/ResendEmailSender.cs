using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

using Memoria.Users.Contracts.Abstractions;
using Memoria.Users.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoria.Users.Services;

/// <summary>
/// Real <see cref="IEmailSender"/> backed by the Resend HTTPS API
/// (<c>POST https://api.resend.com/emails</c>). Selected by DI when
/// <c>Email:ApiKey</c> is configured; otherwise the logging stub is used.
/// </summary>
/// <remarks>
/// Deliberately fail-open: transient Resend errors are logged but never thrown.
/// The user UI still says "code sent" because the server has no synchronous
/// signal back. The verification code is never written to logs from this
/// adapter (only metadata) — leaks would weaken the email-OTP flow.
/// </remarks>
internal sealed class ResendEmailSender : IEmailSender
{
    public const string DefaultBaseUrl = "https://api.resend.com";
    private static readonly Uri EmailsEndpoint = new("emails", UriKind.Relative);
    private const int MaxLoggedBody = 500;

    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        HttpClient http,
        IOptions<EmailOptions> options,
        ILogger<ResendEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendVerificationCodeAsync(string email, string code, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            _logger.LogWarning(
                "ResendEmailSender: Email:FromAddress is not configured — verification code for {Email} not sent",
                MaskEmail(email));
            return;
        }

        var body = new JsonObject
        {
            ["from"] = _options.FromAddress,
            ["to"] = new JsonArray { email },
            ["subject"] = "Your Memoria sign-in code",
            ["text"] = BuildPlainText(code),
            ["html"] = BuildHtml(code),
        };

        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(EmailsEndpoint, content, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Resend request failed for {Email}", MaskEmail(email));
            return;
        }
        catch (TaskCanceledException ex)
        {
            if (ct.IsCancellationRequested) throw;
            _logger.LogWarning(ex, "Resend request timed out for {Email}", MaskEmail(email));
            return;
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Resend accepted verification code for {Email} (status {Status})",
                    MaskEmail(email), (int)response.StatusCode);
                return;
            }

            var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogWarning(
                "Resend returned {Status} for {Email}: {Body}",
                (int)response.StatusCode, MaskEmail(email), Truncate(payload));
        }
    }

    private static string BuildPlainText(string code) =>
        $"Your Memoria sign-in code is: {code}\n\nThe code expires in 10 minutes. " +
        "If you did not request a sign-in, ignore this message.";

    private static string BuildHtml(string code) =>
        $"""
        <!doctype html>
        <html>
          <body style="margin:0;padding:24px;background:#f8fafc;font-family:ui-sans-serif,system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;color:#0f172a;">
            <div style="max-width:480px;margin:0 auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:12px;padding:32px;">
              <h1 style="margin:0 0 16px;font-size:20px;font-weight:600;">Memoria sign-in</h1>
              <p style="margin:0 0 24px;font-size:14px;color:#475569;line-height:1.5;">
                Enter this code to finish signing in. It expires in 10 minutes.
              </p>
              <div style="font-size:28px;font-weight:600;letter-spacing:0.4em;text-align:center;padding:16px;background:#f1f5f9;border-radius:8px;">{code}</div>
              <p style="margin:24px 0 0;font-size:12px;color:#94a3b8;line-height:1.5;">
                If you did not request a sign-in, you can ignore this message.
              </p>
            </div>
          </body>
        </html>
        """;

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at < 1) return "***";
        var local = email[..at];
        var domain = email[(at + 1)..];
        var shown = local.Length <= 2 ? local[..1] : local[..2];
        return $"{shown}***@{domain}";
    }

    private static string Truncate(string s) =>
        s.Length <= MaxLoggedBody ? s : s[..MaxLoggedBody];
}
