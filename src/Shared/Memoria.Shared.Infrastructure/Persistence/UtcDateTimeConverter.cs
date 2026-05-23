using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Memoria.Shared.Infrastructure.Persistence;

/// <summary>
/// Гарантирует, что DateTime, прочитанный из БД, имеет <see cref="DateTimeKind.Utc"/>,
/// а при записи приводится к UTC. Применять ко всем DateTime/DateTime? в EF-конфигурациях
/// либо через convention в DbContext.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}

/// <summary>
/// То же самое для nullable DateTime.
/// </summary>
public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            value => value.HasValue
                ? (value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime())
                : null,
            value => value.HasValue
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : null)
    {
    }
}