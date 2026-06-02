namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// Input for validating that a Question card's body coherently answers its
/// title. UserId carries through so usage is attributed correctly via
/// <c>AiUsageRecorded</c>.
/// </summary>
public sealed record QuestionCardValidationRequest(
    Guid UserId,
    string Question,
    string Body);
