using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.Quota;

/// <summary>
/// Default <see cref="IAiQuotaService"/> impl: every check succeeds.
/// Keeps the production behaviour unchanged while the real meter is
/// being built — swap this registration for the metering impl once it
/// lands and the graders pick it up unchanged.
/// </summary>
internal sealed class AlwaysAllowAiQuotaService : IAiQuotaService
{
    public Task<Result<Unit>> EnsureQuotaAvailableAsync(
        Guid userId,
        AiOperation operation,
        CancellationToken ct) =>
        Task.FromResult(Result<Unit>.Success(Unit.Value));
}
