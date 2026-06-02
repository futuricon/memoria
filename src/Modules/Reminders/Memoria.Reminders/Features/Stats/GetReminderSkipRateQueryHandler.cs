using MediatR;

using Memoria.Reminders.Contracts.Dtos;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.Features.Stats;

internal sealed class GetReminderSkipRateQueryHandler
    : IRequestHandler<GetReminderSkipRateQuery, Result<ReminderSkipRateDto>>
{
    private const int MaxDaysBack = 365;

    private readonly RemindersDbContext _db;
    private readonly TimeProvider _clock;

    public GetReminderSkipRateQueryHandler(RemindersDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<Result<ReminderSkipRateDto>> Handle(
        GetReminderSkipRateQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var daysBack = Math.Clamp(request.DaysBack, 1, MaxDaysBack);
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-daysBack);

        // Bucket by ScheduledAt — the reminder's intended cycle — so a long
        // delay between scheduling and confirmation doesn't push a row out of
        // the window. We only care about reminders that left Pending: Sent,
        // Confirmed, Skipped, Failed.
        var counts = await _db.Reminders
            .Where(r => r.ScheduledAt >= cutoff
                        && r.Status != ReminderStatus.Pending
                        && r.Status != ReminderStatus.Sending
                        && r.Status != ReminderStatus.Cancelled)
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        int Pick(ReminderStatus s) => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;

        return Result<ReminderSkipRateDto>.Success(new ReminderSkipRateDto(
            Sent: Pick(ReminderStatus.Sent),
            Confirmed: Pick(ReminderStatus.Confirmed),
            Skipped: Pick(ReminderStatus.Skipped),
            Failed: Pick(ReminderStatus.Failed)));
    }
}
