using Memoria.AI.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.Contracts.Abstractions;

/// <summary>
/// Port for validating that a Question card's body coherently answers its
/// title. A failed <see cref="Result{T}"/> means the validator was unavailable
/// (network/quota/provider error) — distinct from a successful validation that
/// reports <see cref="QuestionCardValidation.IsCoherent"/> = false.
/// </summary>
public interface IQuestionCardValidator
{
    Task<Result<QuestionCardValidation>> ValidateAsync(
        QuestionCardValidationRequest request,
        CancellationToken ct);
}
