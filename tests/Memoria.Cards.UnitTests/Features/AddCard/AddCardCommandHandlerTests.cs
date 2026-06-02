using System.Diagnostics.CodeAnalysis;
using FluentAssertions;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Contracts.Dtos;
using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Events;
using Memoria.Cards.Features.AddCard;
using Memoria.Cards.Persistence;
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
    private readonly IQuestionCardValidator _questionValidator = Substitute.For<IQuestionCardValidator>();

    private AddCardCommandHandler CreateSut(CardsDbContext db) =>
        new(db, _normalizer, new TagRepository(db, _clock), _clock, _publisher,
            _questionValidator, NullLogger<AddCardCommandHandler>.Instance);

    private void StubValidation(bool isCoherent, string? reason = null) =>
        _questionValidator
            .ValidateAsync(Arg.Any<QuestionCardValidationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<QuestionCardValidation>.Success(new QuestionCardValidation(isCoherent, reason)));

    private void StubValidationUnavailable() =>
        _questionValidator
            .ValidateAsync(Arg.Any<QuestionCardValidationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<QuestionCardValidation>.Failure(Error.Unexpected("ai.request_failed", "down")));

    [Fact]
    public async Task HandleWithValidInputCreatesCardWithNormalizedTags()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var sut = CreateSut(db);

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
        var sut = CreateSut(db);

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
        var sut = CreateSut(db);

        var result = await sut.Handle(
            new AddCardCommand(userId, "Title", "Body", new[] { "ok-tag", "bad#tag" }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        (await db.Cards.CountAsync()).Should().Be(0, "transaction must not persist anything on validation failure");
    }

    [Fact]
    public async Task HandleWithTooManyTagsReturnsValidation()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var sut = CreateSut(db);

        var result = await sut.Handle(
            new AddCardCommand(userId, "Title", "Body", new[] { "a1", "b2", "c3", "d4", "e5", "f6" }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("cards.too_many_tags");
        (await db.Cards.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HandlePublishesCardCreatedEvent()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var sut = CreateSut(db);

        await sut.Handle(new AddCardCommand(userId, "T", "B", new[] { "tag1" }), CancellationToken.None);

        await _publisher.Received(1)
            .Publish(Arg.Any<CardCreatedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleNoteTitleCreatesNoteCardWithoutCallingValidator()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var sut = CreateSut(db);

        var result = await sut.Handle(
            new AddCardCommand(userId, "PostgreSQL VACUUM", "body", Array.Empty<string>()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Type.Should().Be(CardType.Note);
        await _questionValidator.DidNotReceive()
            .ValidateAsync(Arg.Any<QuestionCardValidationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleQuestionTitleWithCoherentBodyCreatesQuestionCard()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        StubValidation(isCoherent: true);
        var sut = CreateSut(db);

        var result = await sut.Handle(
            new AddCardCommand(userId, "What does VACUUM do?", "Reclaims dead tuples.", Array.Empty<string>()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Type.Should().Be(CardType.Question);
        (await db.Cards.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HandleQuestionTitleWithIncoherentBodyReturnsValidationError()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        StubValidation(isCoherent: false, reason: "Body is unrelated to the question.");
        var sut = CreateSut(db);

        var result = await sut.Handle(
            new AddCardCommand(userId, "What does VACUUM do?", "My favorite color is blue.", Array.Empty<string>()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("cards.question_body_mismatch");
        result.Error.Message.Should().Be("Body is unrelated to the question.");
        (await db.Cards.CountAsync()).Should().Be(0, "incoherent Question card must not be created");
    }

    [Fact]
    public async Task HandleQuestionValidationUnavailableCreatesCardFailOpen()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        StubValidationUnavailable();
        var sut = CreateSut(db);

        var result = await sut.Handle(
            new AddCardCommand(userId, "What does VACUUM do?", "Reclaims dead tuples.", Array.Empty<string>()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("AI outage must not block card creation (fail-open)");
        result.Value!.Type.Should().Be(CardType.Question);
        (await db.Cards.CountAsync()).Should().Be(1);
    }
}
