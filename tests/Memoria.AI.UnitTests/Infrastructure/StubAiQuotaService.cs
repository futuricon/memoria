using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.UnitTests.Infrastructure;

/// <summary>
/// In-memory <see cref="IAiQuotaService"/> for grader / validator unit
/// tests. Defaults to <see cref="AlwaysAllow"/> (matches production default),
/// or use <see cref="AlwaysBlock"/> to exercise the fail-closed branch.
/// </summary>
internal sealed class StubAiQuotaService : IAiQuotaService
{
    private readonly Result<Unit> _result;

    private StubAiQuotaService(Result<Unit> result) => _result = result;

    public static StubAiQuotaService AlwaysAllow() =>
        new(Result<Unit>.Success(Unit.Value));

    public static StubAiQuotaService AlwaysBlock(Error error) =>
        new(Result<Unit>.Failure(error));

    public int CallCount { get; private set; }

    public Task<Result<Unit>> EnsureQuotaAvailableAsync(
        Guid userId, AiOperation operation, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(_result);
    }
}
