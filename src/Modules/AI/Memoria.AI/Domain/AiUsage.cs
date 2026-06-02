using Memoria.AI.Contracts.Dtos;

namespace Memoria.AI.Domain;

/// <summary>
/// Append-only record of a single AI provider call. One row per
/// grader / validator invocation, success or failure. Powers admin spend
/// analytics and the per-user monthly quota meter (Phase 5).
/// </summary>
internal sealed class AiUsage
{
    private AiUsage()
    {
    }

    public AiUsage(
        Guid userId,
        AiOperation operation,
        string model,
        int inputTokens,
        int outputTokens,
        bool isFailure,
        DateTime occurredAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Operation = operation;
        Model = model;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        IsFailure = isFailure;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public AiOperation Operation { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public bool IsFailure { get; private set; }
    public DateTime OccurredAt { get; private set; }
}
