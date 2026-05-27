namespace Memoria.Shared.Kernel.Observability;

/// <summary>
/// Per-operation context that propagates across logical async boundaries via
/// <see cref="OperationContextAccessor"/>. Initialized at the entry point of a
/// request (ASP.NET middleware, Telegram update scope, Hangfire job) and
/// consumed downstream by MediatR pipeline behaviors and Serilog enrichers
/// to tag every log line with the originating operation.
/// </summary>
public sealed class OperationContext
{
    public required string CorrelationId { get; init; }
    public string? Module { get; init; }
    public string? UserId { get; init; }
    public string? TelegramUserId { get; init; }
}
