using FluentAssertions;

using MediatR;

using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Contracts.Dtos;
using Memoria.Bot.Conversations;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Reviews.Contracts.Commands;
using Memoria.Reviews.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace Memoria.Bot.UnitTests.Conversations;

public sealed class AwaitingAnswerHandlerTests
{
    private const long ChatId = 1234;
    private const long TelegramId = 5678;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly Guid ReminderId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITelegramBotClient _client = Substitute.For<ITelegramBotClient>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IAnswerGrader _grader = Substitute.For<IAnswerGrader>();
    private readonly InMemoryConversationStateStore _store = new();
    private readonly AwaitingAnswerHandler _sut;

    public AwaitingAnswerHandlerTests()
    {
        var resolver = new CurrentUserResolver(_mediator);
        _sut = new AwaitingAnswerHandler(
            _client, _mediator, _grader, resolver, _store,
            NullLogger<AwaitingAnswerHandler>.Instance);

        _mediator
            .Send(Arg.Any<GetUserByTelegramIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserIdentityResolutionDto>.Success(
                new UserIdentityResolutionDto(UserId, "Tester", null)));

        // The handler sends a "⏳ checking…" message and reads its MessageId,
        // so the underlying SendRequest must return a real Message.
        _client
            .SendRequest(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Message { Id = 1 });
    }

    private static Message MakeMessage(string text) => new()
    {
        Chat = new Chat { Id = ChatId },
        From = new User { Id = TelegramId },
        Text = text,
    };

    private static AwaitingAnswerState State() => new(ReminderId, CardId);

    private void StubCard() =>
        _mediator
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CardDto>.Success(new CardDto(
                CardId, "What does VACUUM do?", "Reclaims dead tuples.",
                Array.Empty<string>(), Now, Now, CardType.Question)));

    private void StubRecordReview() =>
        _mediator
            .Send(Arg.Any<RecordReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReviewDto>.Success(new ReviewDto(
                Guid.NewGuid(), CardId, UserId, ReminderId, Rating.Easy,
                "What does VACUUM do?", Now, null)));

    [Fact]
    public async Task HandleGradesAnswerRecordsAutoGradedReviewAndClearsState()
    {
        _store.Start(ChatId, State());
        StubCard();
        StubRecordReview();
        _grader
            .GradeAsync(Arg.Any<GradingRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<GradingResult>.Success(new GradingResult(90, GradingVerdict.Correct, "Nice.")));

        await _sut.HandleAsync(MakeMessage("It frees dead rows."), State(), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<RecordReviewCommand>(c =>
                c.CardId == CardId &&
                c.ReminderId == ReminderId &&
                c.AutoGraded &&
                c.AiScore == 90 &&
                c.Rating == Rating.Easy &&
                c.AnswerText == "It frees dead rows."),
            Arg.Any<CancellationToken>());

        _store.TryGet(ChatId, out _).Should().BeFalse("state is cleared after a successful grade");
    }

    [Fact]
    public async Task HandleTooLongAnswerRePromptsAndKeepsState()
    {
        _store.Start(ChatId, State());
        var longAnswer = new string('x', 2001);

        await _sut.HandleAsync(MakeMessage(longAnswer), State(), CancellationToken.None);

        await _grader.DidNotReceive().GradeAsync(Arg.Any<GradingRequest>(), Arg.Any<CancellationToken>());
        _store.TryGet(ChatId, out _).Should().BeTrue("awaiting state must survive an over-long answer");
    }

    [Fact]
    public async Task HandleGraderUnavailableKeepsStateAndDoesNotRecord()
    {
        _store.Start(ChatId, State());
        StubCard();
        _grader
            .GradeAsync(Arg.Any<GradingRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<GradingResult>.Failure(Error.Unexpected("ai.timeout", "down")));

        await _sut.HandleAsync(MakeMessage("an answer"), State(), CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<RecordReviewCommand>(), Arg.Any<CancellationToken>());
        _store.TryGet(ChatId, out _).Should().BeTrue("transient AI outage should let the user retry");
    }
}
