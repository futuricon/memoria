namespace Memoria.AI.Quota;

/// <summary>
/// Per-user monthly token budget config (section <c>Ai:Quota</c>). The
/// default impl <see cref="AlwaysAllowAiQuotaService"/> ignores these and
/// always succeeds — the values exist now so a real meter can read them
/// without another schema change.
/// <para>
/// TODO: personal overrides via an <c>ai_quota_overrides</c> table land
/// in a follow-up. Until then, <see cref="MonthlyTokenBudget"/> is the
/// flat per-user cap.
/// </para>
/// </summary>
public sealed class AiQuotaOptions
{
    public const string SectionName = "Ai:Quota";

    /// <summary>
    /// Total tokens (input + output) a user may spend in a calendar month
    /// before <c>EnsureQuotaAvailableAsync</c> starts returning
    /// <c>Error.Forbidden("ai.quota_exceeded", …)</c>. 0 = unlimited.
    /// </summary>
    public long MonthlyTokenBudget { get; init; }
}
