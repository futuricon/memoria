using MediatR;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.Features.ReassignRemindersOwner;

/// <summary>
/// Re-parents every reminder owned by <c>SourceUserId</c> to
/// <c>TargetUserId</c>. Load-and-update rather than <c>ExecuteUpdate</c> so
/// the InMemory provider used in unit tests works the same way as Npgsql.
/// </summary>
internal sealed class ReassignRemindersOwnerCommandHandler
    : IRequestHandler<ReassignRemindersOwnerCommand, Result<int>>
{
    private readonly RemindersDbContext _db;

    public ReassignRemindersOwnerCommandHandler(RemindersDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<int>> Handle(ReassignRemindersOwnerCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceUserId == request.TargetUserId)
        {
            return Result<int>.Success(0);
        }

        var reminders = await _db.Reminders
            .Where(r => r.UserId == request.SourceUserId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var reminder in reminders)
        {
            reminder.ReassignTo(request.TargetUserId);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<int>.Success(reminders.Count);
    }
}
