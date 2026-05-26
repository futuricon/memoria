using MediatR;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Options;
using Memoria.Cards.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoria.Cards.Jobs;

/// <summary>
/// Hangfire recurring job that hard-deletes cards whose <c>DeletedAt</c> is
/// older than <see cref="CardsOptions.SoftDeleteRetentionDays"/>. Per
/// addendum §5.2, this is the second hard-delete path next to explicit
/// <see cref="PermanentlyDeleteCardCommand"/>.
/// </summary>
public sealed class PurgeExpiredSoftDeletesJob
{
    private readonly CardsDbContext _db;
    private readonly IMediator _mediator;
    private readonly TimeProvider _clock;
    private readonly CardsOptions _options;
    private readonly ILogger<PurgeExpiredSoftDeletesJob> _logger;

    public PurgeExpiredSoftDeletesJob(
        CardsDbContext db,
        IMediator mediator,
        TimeProvider clock,
        IOptions<CardsOptions> options,
        ILogger<PurgeExpiredSoftDeletesJob> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _mediator = mediator;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-_options.SoftDeleteRetentionDays);

        var expired = await _db.Cards
            .IgnoreQueryFilters()
            .Where(c => c.DeletedAt != null && c.DeletedAt < cutoff)
            .Select(c => new { c.Id, c.UserId })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var x in expired)
        {
            var result = await _mediator
                .Send(new PermanentlyDeleteCardCommand(x.UserId, x.Id), ct)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "PurgeExpiredSoftDeletesJob: failed for card {CardId}: {ErrorCode} — {ErrorMessage}",
                    x.Id, result.Error!.Code, result.Error.Message);
            }
        }

        _logger.LogInformation(
            "PurgeExpiredSoftDeletesJob: completed, {Count} cards processed (cutoff {Cutoff:O})",
            expired.Count, cutoff);
    }
}
