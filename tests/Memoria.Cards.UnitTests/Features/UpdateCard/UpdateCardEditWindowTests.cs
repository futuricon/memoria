using FluentAssertions;

using Microsoft.Extensions.Time.Testing;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Domain;
using Memoria.Cards.Features.UpdateCard;
using Memoria.Cards.Persistence;
using Memoria.Cards.Services;
using Memoria.Cards.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.UnitTests.Features.UpdateCard;

public sealed class UpdateCardEditWindowTests
{
    private static readonly DateTime CardCreatedAt = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    private static UpdateCardCommandHandler CreateSut(CardsDbContext db, DateTimeOffset now)
    {
        var clock = new FakeTimeProvider(now);
        return new UpdateCardCommandHandler(db, new TagNormalizer(), new TagRepository(db, clock), clock);
    }

    [Fact]
    public async Task HandleWithinEditWindowAllowsEdit()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "Old title", "Old body", CardCreatedAt);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        // 23 hours later — still inside the 24 h window.
        var now = new DateTimeOffset(CardCreatedAt.AddHours(23), TimeSpan.Zero);
        var sut = CreateSut(db, now);

        var result = await sut.Handle(
            new UpdateCardCommand(userId, card.Id, "New title", null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("New title");
    }

    [Fact]
    public async Task HandleAfterEditWindowReturnsValidationError()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "Old title", "Old body", CardCreatedAt);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        // 25 hours later — past the 24 h window.
        var now = new DateTimeOffset(CardCreatedAt.AddHours(25), TimeSpan.Zero);
        var sut = CreateSut(db, now);

        var result = await sut.Handle(
            new UpdateCardCommand(userId, card.Id, "New title", null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("cards.edit_window_closed");
    }
}
