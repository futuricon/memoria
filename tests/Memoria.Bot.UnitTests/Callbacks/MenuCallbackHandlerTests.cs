using FluentAssertions;

using MediatR;

using Memoria.Bot.Callbacks;
using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Telegram.Bot;
using Telegram.Bot.Types;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Bot.UnitTests.Callbacks;

public sealed class MenuCallbackHandlerTests
{
    private const long ChatId = 1234;
    private const long TelegramId = 5678;
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly ITelegramBotClient _client = Substitute.For<ITelegramBotClient>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    public MenuCallbackHandlerTests()
    {
        _mediator
            .Send(Arg.Any<GetUserByTelegramIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserIdentityResolutionDto>.Success(
                new UserIdentityResolutionDto(UserId, "Tester", null)));
        _mediator
            .Send(Arg.Any<GetUserPreferencesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserPreferencesDto>.Success(
                new UserPreferencesDto(UserId, "Europe/Moscow", null, null)));
        _mediator
            .Send(Arg.Any<UpdateUserPreferencesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(Unit.Value));
    }

    private MenuCallbackHandler CreateSut(params ITextCommandHandler[] commands) =>
        new(_client, _mediator, new CurrentUserResolver(_mediator), commands,
            NullLogger<MenuCallbackHandler>.Instance);

    private static CallbackQuery MakeCallback(string data) => new()
    {
        Id = "cb1",
        Data = data,
        From = new User { Id = TelegramId },
        Message = new Message { Id = 10, Chat = new Chat { Id = ChatId } },
    };

    [Fact]
    public async Task QuietSetPresetUpdatesPreferencesPreservingTimezone()
    {
        var sut = CreateSut();

        await sut.HandleAsync(MakeCallback("menu:quietset:2200-0800"), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateUserPreferencesCommand>(c =>
                c.UserId == UserId &&
                c.TimeZoneId == "Europe/Moscow" &&
                c.QuietHoursStart == new TimeOnly(22, 0) &&
                c.QuietHoursEnd == new TimeOnly(8, 0)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuietSetOffClearsQuietHours()
    {
        var sut = CreateSut();

        await sut.HandleAsync(MakeCallback("menu:quietset:off"), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateUserPreferencesCommand>(c =>
                c.QuietHoursStart == null && c.QuietHoursEnd == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuietSetBadTokenDoesNotUpdate()
    {
        var sut = CreateSut();

        await sut.HandleAsync(MakeCallback("menu:quietset:nonsense"), CancellationToken.None);

        await _mediator.DidNotReceive().Send(
            Arg.Any<UpdateUserPreferencesCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionButtonDelegatesToMatchingCommandHandler()
    {
        var addHandler = Substitute.For<ITextCommandHandler>();
        addHandler.CommandName.Returns("add");
        var sut = CreateSut(addHandler);

        await sut.HandleAsync(MakeCallback("menu:add"), CancellationToken.None);

        await addHandler.Received(1).HandleAsync(
            Arg.Is<Message>(m => m.Chat.Id == ChatId && m.From!.Id == TelegramId),
            null,
            Arg.Any<CancellationToken>());
    }
}
