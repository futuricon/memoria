namespace Memoria.Users.Options;

/// <summary>
/// Resend transactional-email adapter configuration. When <see cref="ApiKey"/>
/// is blank, DI falls back to the logging stub (verification codes go to the
/// app log instead of real email). In production both fields must be set.
/// </summary>
internal sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Resend API key (`re_...`). Blank = stub-mode (logs only).
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// "From" header for outgoing mail, e.g.
    /// <c>Memoria &lt;noreply@memoria.futuricon.net&gt;</c>. The domain must be
    /// verified in the Resend dashboard.
    /// </summary>
    public string? FromAddress { get; init; }
}
