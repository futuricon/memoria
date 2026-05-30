using MediatR;

using Memoria.Reminders.Contracts.Queries;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.Features.GetCurrentCardStage;

internal sealed class GetCurrentCardStageQueryHandler
    : IRequestHandler<GetCurrentCardStageQuery, Result<int?>>
{
    private readonly RemindersDbContext _db;

    public GetCurrentCardStageQueryHandler(RemindersDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<int?>> Handle(GetCurrentCardStageQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var max = await _db.Reminders
            .Where(r => r.CardId == request.CardId)
            .Select(r => (int?)r.StageNumber)
            .MaxAsync(ct)
            .ConfigureAwait(false);

        return Result<int?>.Success(max);
    }
}
