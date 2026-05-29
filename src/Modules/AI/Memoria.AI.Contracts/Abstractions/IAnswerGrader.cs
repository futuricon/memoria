using Memoria.AI.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.Contracts.Abstractions;

/// <summary>
/// Port for grading a free-text answer against a reference answer. A failed
/// <see cref="Result{T}"/> means the grader was unavailable (network/quota/
/// provider error) — callers decide how to degrade.
/// </summary>
public interface IAnswerGrader
{
    Task<Result<GradingResult>> GradeAsync(GradingRequest request, CancellationToken ct);
}
