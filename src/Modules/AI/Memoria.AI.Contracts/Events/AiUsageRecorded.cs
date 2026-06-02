using MediatR;

using Memoria.AI.Contracts.Dtos;

namespace Memoria.AI.Contracts.Events;

/// <summary>
/// Published by the AI grader / validator after every LLM call (success or
/// failure). Picked up internally by the AI module to persist an append-only
/// <c>ai_usage</c> row for analytics. Future subscribers can hook this for
/// real-time quota enforcement or alerting without touching the grading flow.
/// </summary>
/// <param name="UserId">Who triggered the call.</param>
/// <param name="Operation">Which AI flow this was (grading vs validation).</param>
/// <param name="Model">Provider-resolved model name (e.g.
/// "claude-sonnet-4-6", "deepseek-chat").</param>
/// <param name="InputTokens">Tokens billed for the prompt; zero on failures
/// or when the provider didn't return a usage block.</param>
/// <param name="OutputTokens">Tokens billed for the completion; zero on
/// failures.</param>
/// <param name="IsFailure">True when the underlying LLM call failed (any
/// transport, status, or parsing error). Lets the dashboard compute
/// failure rate without joining a second table.</param>
/// <param name="OccurredAt">UTC instant the call resolved.</param>
public sealed record AiUsageRecorded(
    Guid UserId,
    AiOperation Operation,
    string Model,
    int InputTokens,
    int OutputTokens,
    bool IsFailure,
    DateTime OccurredAt) : INotification;
