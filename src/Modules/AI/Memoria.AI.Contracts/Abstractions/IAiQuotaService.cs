using Memoria.AI.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.Contracts.Abstractions;

/// <summary>
/// Per-user AI spend gate. Graders call <see cref="EnsureQuotaAvailableAsync"/>
/// before every LLM round-trip; a <c>Failure</c> result prevents the call.
/// <para>
/// Intentional fail-CLOSED posture: quota errors short-circuit the grader
/// even though grader's normal stance is fail-open on infra issues. The
/// rationale: a quota check failing means we either don't know the user's
/// remaining budget or they've exhausted it — both warrant blocking, since
/// silently letting calls through defeats the purpose of having a quota.
/// </para>
/// </summary>
public interface IAiQuotaService
{
    Task<Result<Unit>> EnsureQuotaAvailableAsync(
        Guid userId,
        AiOperation operation,
        CancellationToken ct);
}
