using FluentAssertions;

using MediatR;

using Memoria.Bot.Adapters;
using Memoria.Reminders.Contracts.Abstractions;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.UnitTests.Adapters;

public sealed class TelegramReminderNotificationSenderTests
{
    private readonly ITelegramBotClient _client = Substitute.For<ITelegramBotClient>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly TelegramReminderNotificationSender _sut;

    private static readonly Guid ReminderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CardId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public TelegramReminderNotificationSenderTests()
    {
        _sut = new TelegramReminderNotificationSender(_client, _mediator, NullLogger<TelegramReminderNotificationSender>.Instance);
    }

    private static ReminderNotification SampleNotification() => new(
        ReminderId, UserId, CardId,
        CardTitle: "PostgreSQL VACUUM",
        CardBody: "How does VACUUM work?",
        Tags: new[] { "postgres" },
        StageNumber: 2);

    private void StubIdentitiesResult(Result<IReadOnlyList<UserIdentityDto>> result)
    {
        _mediator
            .Send(Arg.Any<GetUserIdentitiesQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    private void StubSendMessageReturns(Message sent)
    {
        _client
            .SendRequest(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(sent);
    }

    private void StubSendMessageThrows(Exception ex)
    {
        _client
            .SendRequest(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Throws(ex);
    }

    [Fact]
    public async Task SendReminderAsyncResolvesTelegramIdAndSends()
    {
        StubIdentitiesResult(Result<IReadOnlyList<UserIdentityDto>>.Success(new[]
        {
            new UserIdentityDto("Telegram", "12345", DateTime.UtcNow),
        }));
        var fakeMessage = new Message { Id = 99 };
        StubSendMessageReturns(fakeMessage);

        var result = await _sut.SendReminderAsync(SampleNotification(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _client.Received(1).SendRequest(
            Arg.Is<SendMessageRequest>(r => r.ChatId.Identifier == 12345L),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendReminderAsyncReturnsFailureWhenUserHasNoTelegramIdentity()
    {
        StubIdentitiesResult(Result<IReadOnlyList<UserIdentityDto>>.Success(new[]
        {
            new UserIdentityDto("Google", "user@example.com", DateTime.UtcNow),
        }));

        var result = await _sut.SendReminderAsync(SampleNotification(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("bot.no_telegram_identity");
    }

    [Fact]
    public async Task SendReminderAsyncReturnsFailureWhenIdentitiesQueryFails()
    {
        StubIdentitiesResult(Result<IReadOnlyList<UserIdentityDto>>.Failure(
            Error.NotFound("users.not_found", "missing")));

        var result = await _sut.SendReminderAsync(SampleNotification(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("users.not_found");
    }

    [Fact]
    public async Task SendReminderAsyncIncludesShowAndSkipKeyboardButtons()
    {
        StubIdentitiesResult(Result<IReadOnlyList<UserIdentityDto>>.Success(new[]
        {
            new UserIdentityDto("Telegram", "12345", DateTime.UtcNow),
        }));
        StubSendMessageReturns(new Message { Id = 1 });

        await _sut.SendReminderAsync(SampleNotification(), CancellationToken.None);

        await _client.Received(1).SendRequest(
            Arg.Is<SendMessageRequest>(r =>
                ContainsCallback(r.ReplyMarkup, "show") &&
                ContainsCallback(r.ReplyMarkup, "skip")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendReminderAsyncReturnsMessageIdOnSuccess()
    {
        StubIdentitiesResult(Result<IReadOnlyList<UserIdentityDto>>.Success(new[]
        {
            new UserIdentityDto("Telegram", "12345", DateTime.UtcNow),
        }));
        StubSendMessageReturns(new Message { Id = 42 });

        var result = await _sut.SendReminderAsync(SampleNotification(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task SendReminderAsyncOn429ReturnsRateLimitError()
    {
        StubIdentitiesResult(Result<IReadOnlyList<UserIdentityDto>>.Success(new[]
        {
            new UserIdentityDto("Telegram", "12345", DateTime.UtcNow),
        }));
        StubSendMessageThrows(new ApiRequestException(message: "Too Many Requests", errorCode: 429));

        var result = await _sut.SendReminderAsync(SampleNotification(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("bot.telegram_rate_limit");
    }

    private static bool ContainsCallback(IReplyMarkup? markup, string ratingTag)
    {
        if (markup is not InlineKeyboardMarkup ik) return false;
        return ik.InlineKeyboard.Any(row => row.Any(b =>
            b.CallbackData?.Contains(":" + ratingTag, StringComparison.Ordinal) == true));
    }
}
