using MediatR;

using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Contracts.Queries;

/// <summary>
/// Резолвит карточку по короткому hex-префиксу её GUID-а
/// (<see cref="Guid.ToString(string?)"/> с форматом "N"). Используется ботом,
/// где в чате удобнее показывать <c>a1b2c3d4</c> вместо полного GUID-а.
/// </summary>
public sealed record ResolveCardByPrefixQuery(Guid UserId, string Prefix)
    : IRequest<Result<Guid>>;
