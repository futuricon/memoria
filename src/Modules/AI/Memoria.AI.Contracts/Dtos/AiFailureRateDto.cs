namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// AI call success / failure counts for a trailing window. Failure rate is
/// the share of <c>IsFailure = true</c> rows in <c>ai.ai_usage</c>.
/// </summary>
public sealed record AiFailureRateDto(
    int TotalCalls,
    int FailedCalls)
{
    public double FailureRate => TotalCalls == 0 ? 0d : (double)FailedCalls / TotalCalls;
}
