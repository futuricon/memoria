using MediatR;

using Memoria.Cards.Contracts.Queries;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Bot.Services;

/// <summary>
/// Тонкая обёртка над <see cref="ResolveCardByPrefixQuery"/> — отделяет
/// бот-handler-ы от прямого <see cref="IMediator"/>-вызова. Возвращает тот же
/// <see cref="Result{T}"/>, что и query, чтобы callers могли сразу мапить
/// тип ошибки на user-facing сообщение.
/// </summary>
internal sealed class CardIdResolver
{
    private readonly IMediator _mediator;

    public CardIdResolver(IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        _mediator = mediator;
    }

    public Task<Result<Guid>> ResolveAsync(Guid userId, string prefix, CancellationToken ct)
    {
        return _mediator.Send(new ResolveCardByPrefixQuery(userId, prefix), ct);
    }
}
