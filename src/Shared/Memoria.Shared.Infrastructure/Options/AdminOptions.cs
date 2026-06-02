namespace Memoria.Shared.Infrastructure.Options;

/// <summary>
/// Bootstrap list of email addresses that should automatically be promoted to
/// <c>Role.Admin</c> at every token-issuance handler. There's no admin
/// management UI by design — operator emails come from configuration, same
/// pattern as <c>HangfireDashboardOptions.AllowedEmails</c>.
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string[] Emails { get; init; } = Array.Empty<string>();
}
