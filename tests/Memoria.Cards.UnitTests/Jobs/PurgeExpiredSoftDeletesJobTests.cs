using FluentAssertions;

using MediatR;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Domain;
using Memoria.Cards.Jobs;
using Memoria.Cards.Options;
using Memoria.Cards.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.UnitTests.Jobs;

public sealed class PurgeExpiredSoftDeletesJobTests
{
    private static readonly DateTime NowUtc = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(NowUtc, TimeSpan.Zero));
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly CardsOptions _options = new() { SoftDeleteRetentionDays = 90 };

    private PurgeExpiredSoftDeletesJob CreateSut(Persistence.CardsDbContext db) =>
        new(db,
            _mediator,
            _clock,
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<PurgeExpiredSoftDeletesJob>.Instance);

    private static Card NewCardSoftDeletedAt(DateTime deletedAtUtc)
    {
        var card = new Card(Guid.NewGuid(), "Title", "Body", NowUtc.AddDays(-200));
        card.SoftDelete(deletedAtUtc);
        return card;
    }

    [Fact]
    public async Task ExecuteAsyncDeletesCardsOlderThanCutoff()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var freshActive = new Card(Guid.NewGuid(), "Active", "Body", NowUtc.AddDays(-10));
        var recentlyDeleted = NewCardSoftDeletedAt(NowUtc.AddDays(-30));
        var expiredDeleted = NewCardSoftDeletedAt(NowUtc.AddDays(-100));

        db.Cards.AddRange(freshActive, recentlyDeleted, expiredDeleted);
        await db.SaveChangesAsync();

        _mediator
            .Send(Arg.Any<PermanentlyDeleteCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(Unit.Value));

        var sut = CreateSut(db);

        await sut.ExecuteAsync(CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<PermanentlyDeleteCardCommand>(c => c.CardId == expiredDeleted.Id),
            Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive().Send(
            Arg.Is<PermanentlyDeleteCardCommand>(c => c.CardId == recentlyDeleted.Id),
            Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive().Send(
            Arg.Is<PermanentlyDeleteCardCommand>(c => c.CardId == freshActive.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncSkipsCardsNotSoftDeleted()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var active1 = new Card(Guid.NewGuid(), "A", "Body", NowUtc.AddDays(-5));
        var active2 = new Card(Guid.NewGuid(), "B", "Body", NowUtc.AddDays(-200));

        db.Cards.AddRange(active1, active2);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        await sut.ExecuteAsync(CancellationToken.None);

        await _mediator.DidNotReceiveWithAnyArgs().Send(
            Arg.Any<PermanentlyDeleteCardCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncLogsFailuresAndContinues()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var cardA = NewCardSoftDeletedAt(NowUtc.AddDays(-100));
        var cardB = NewCardSoftDeletedAt(NowUtc.AddDays(-150));
        db.Cards.AddRange(cardA, cardB);
        await db.SaveChangesAsync();

        _mediator
            .Send(Arg.Is<PermanentlyDeleteCardCommand>(c => c.CardId == cardA.Id), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Failure(Error.Unexpected("cards.delete_failed", "boom")));
        _mediator
            .Send(Arg.Is<PermanentlyDeleteCardCommand>(c => c.CardId == cardB.Id), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(Unit.Value));

        var sut = CreateSut(db);

        await sut.ExecuteAsync(CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<PermanentlyDeleteCardCommand>(c => c.CardId == cardA.Id),
            Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(
            Arg.Is<PermanentlyDeleteCardCommand>(c => c.CardId == cardB.Id),
            Arg.Any<CancellationToken>());
    }
}
