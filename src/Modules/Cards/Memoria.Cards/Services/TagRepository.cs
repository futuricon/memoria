using Microsoft.EntityFrameworkCore;

using Memoria.Cards.Domain;
using Memoria.Cards.Persistence;

namespace Memoria.Cards.Services;

/// <summary>
/// Резолвит уже-нормализованные теги: ищет существующие в БД, создаёт
/// недостающие, возвращает <see cref="Tag.Id"/> по имени.
/// </summary>
internal sealed class TagRepository
{
    private readonly CardsDbContext _db;
    private readonly TimeProvider _clock;

    public TagRepository(CardsDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Гарантирует наличие тегов для пользователя; возвращает их id-шники
    /// в том же порядке. Сохранение в БД делает caller (через SaveChangesAsync).
    /// </summary>
    public async Task<IReadOnlyList<Guid>> EnsureTagsAsync(
        Guid userId,
        IReadOnlyList<string> normalizedNames,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(normalizedNames);
        if (normalizedNames.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var existing = await _db.Tags
            .Where(t => t.UserId == userId && normalizedNames.Contains(t.NormalizedName))
            .ToDictionaryAsync(t => t.NormalizedName, t => t.Id, ct)
            .ConfigureAwait(false);

        var now = _clock.GetUtcNow().UtcDateTime;
        var result = new List<Guid>(normalizedNames.Count);

        foreach (var name in normalizedNames)
        {
            if (existing.TryGetValue(name, out var id))
            {
                result.Add(id);
                continue;
            }

            var newTag = new Tag(userId, name, now);
            _db.Tags.Add(newTag);
            existing[name] = newTag.Id;
            result.Add(newTag.Id);
        }

        return result;
    }
}
