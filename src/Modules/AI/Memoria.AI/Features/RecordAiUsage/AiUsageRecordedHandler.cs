using MediatR;

using Memoria.AI.Contracts.Events;
using Memoria.AI.Domain;
using Memoria.AI.Persistence;

using Microsoft.Extensions.Logging;

namespace Memoria.AI.Features.RecordAiUsage;

/// <summary>
/// Persists the append-only <c>ai_usage</c> row when the grader / validator
/// publishes <see cref="AiUsageRecorded"/>. Failures are logged and swallowed
/// so a DB blip never propagates into the grading flow (the grader is
/// fail-open on infra issues).
/// </summary>
internal sealed class AiUsageRecordedHandler : INotificationHandler<AiUsageRecorded>
{
    private readonly AiDbContext _db;
    private readonly ILogger<AiUsageRecordedHandler> _logger;

    public AiUsageRecordedHandler(AiDbContext db, ILogger<AiUsageRecordedHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task Handle(AiUsageRecorded notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            var row = new AiUsage(
                notification.UserId,
                notification.Operation,
                notification.Model,
                notification.InputTokens,
                notification.OutputTokens,
                notification.IsFailure,
                notification.OccurredAt);

            _db.Usage.Add(row);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist AiUsage for user {UserId}, op {Operation}, model {Model}",
                notification.UserId,
                notification.Operation,
                notification.Model);
        }
    }
}
