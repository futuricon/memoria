using System.Diagnostics.CodeAnalysis;
using FluentAssertions;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Events;
using Memoria.Cards.Features.AddCard;
using Memoria.Cards.Services;
using Memoria.Cards.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.UnitTests.Features.AddCard;

[SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments")]
public sealed class AddCardCommandHandlerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero));
    private readonly TagNormalizer _normalizer = new();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();

    [Fact]
    public async Task HandleWithValidInputCreatesCardWithNormalizedTags()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var sut = new AddCardCommandHandler(db, _normalizer, new TagRepository(db, _clock), _clock, _publisher);

        var result = await sut.Handle(
            new AddCardCommand(userId, "PostgreSQL", "VACUUM details...", new[] { "PostgreSQL", "Database" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("PostgreSQL");
        result.Value.Tags.Should().BeEquivalentTo(new[] { "postgresql", "database" });

        (await db.Cards.CountAsync()).Should().Be(1);
        (await db.Tags.CountAsync()).Should().Be(2);
        (await db.CardTags.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task HandleReusesExistingTagsForSameUser()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var sut = new AddCardCommandHandler(db, _normalizer, new TagRepository(db, _clock), _clock, _publisher);

        await sut.Handle(new AddCardCommand(userId, "C1", "B1", new[] { "postgres" }), CancellationToken.None);
        await sut.Handle(new AddCardCommand(userId, "C2", "B2", new[] { "Postgres" }), CancellationToken.None);

        (await db.Tags.CountAsync()).Should().Be(1, "tag 'postgres' is reused");
        (await db.CardTags.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task HandleWithInvalidTagReturnsValidation()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var sut = new AddCardCommandHandler(db, _normalizer, new TagRepository(db, _clock), _clock, _publisher);

        var result = await sut.Handle(
            new AddCardCommand(userId, "Title", "Body", new[] { "ok-tag", "bad#tag" }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        (await db.Cards.CountAsync()).Should().Be(0, "transaction must not persist anything on validation failure");
    }

    [Fact]
    public async Task HandlePublishesCardCreatedEvent()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var sut = new AddCardCommandHandler(db, _normalizer, new TagRepository(db, _clock), _clock, _publisher);

        await sut.Handle(new AddCardCommand(userId, "T", "B", new[] { "tag1" }), CancellationToken.None);

        await _publisher.Received(1)
            .Publish(Arg.Any<CardCreatedEvent>(), Arg.Any<CancellationToken>());
    }
}
