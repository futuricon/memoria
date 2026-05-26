using System.Globalization;

using MediatR;

using Memoria.Cards.Contracts.Queries;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Cards.Features.ResolveCardByPrefix;

internal sealed class ResolveCardByPrefixQueryHandler
    : IRequestHandler<ResolveCardByPrefixQuery, Result<Guid>>
{
    private const int MinPrefix = 4;
    private const int MaxPrefix = 32;

    private readonly CardsDbContext _db;

    public ResolveCardByPrefixQueryHandler(CardsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<Guid>> Handle(ResolveCardByPrefixQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prefix = (request.Prefix ?? string.Empty).Trim().ToLowerInvariant();
        if (prefix.Length is < MinPrefix or > MaxPrefix || !IsHex(prefix))
        {
            return Result<Guid>.Failure(Error.Validation(
                "cards.prefix_invalid",
                "ID prefix must be 4–32 hex characters."));
        }

        // Карточек у пользователя обычно немного (<100 в иттерации 1), поэтому
        // тянем только Id, фильтруем in-memory — переводимость StartsWith на
        // Guid.ToString("N") у Npgsql ненадёжна и зависит от провайдера.
        var allIds = await _db.Cards
            .Where(c => c.UserId == request.UserId)
            .Select(c => c.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var matches = allIds
            .Where(id => id.ToString("N", CultureInfo.InvariantCulture).StartsWith(prefix, StringComparison.Ordinal))
            .Take(2)
            .ToList();

        return matches.Count switch
        {
            0 => Result<Guid>.Failure(Error.NotFound("cards.not_found", "No card matches that ID.")),
            1 => Result<Guid>.Success(matches[0]),
            _ => Result<Guid>.Failure(Error.Conflict("cards.prefix_ambiguous",
                "Multiple cards match. Use a longer ID prefix.")),
        };
    }

    private static bool IsHex(ReadOnlySpan<char> s)
    {
        foreach (var c in s)
        {
            var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!isHex) return false;
        }
        return true;
    }
}
