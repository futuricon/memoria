namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Crude retention: of users who signed up between
/// <paramref name="WindowStart"/> and <paramref name="WindowEnd"/>, what
/// fraction were ever seen again at D1 / D7 / D30 (i.e. their
/// <c>LastSeenAt</c> is at least N days after <c>CreatedAt</c>).
/// </summary>
public sealed record RetentionCohortsDto(
    DateTime WindowStart,
    DateTime WindowEnd,
    int Signups,
    int D1Retained,
    int D7Retained,
    int D30Retained);
