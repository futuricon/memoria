using System.Globalization;

using FluentAssertions;

using MediatR;

using Memoria.Bot.Callbacks;
using Memoria.Bot.Commands;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.UnitTests.Callbacks;

public sealed class CardsBrowseCallbackHandlerTests
{
    private const long ChatId = 1234;
    private const long TelegramId = 5678;
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITelegramBotClient _client = Substitute.For<ITelegramBotClient>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    public CardsBrowseCallbackHandlerTests()
    {
        _mediator
            .Send(Arg.Any<GetUserByTelegramIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserIdentityResolutionDto>.Success(
                new UserIdentityResolutionDto(UserId, "Tester", null)));
    }

    private CardsBrowseCallbackHandler CreateSut()
    {
        var resolver = new CurrentUserResolver(_mediator);
        var list = new ListCommandHandler(_client, _mediator, resolver);
        return new CardsBrowseCallbackHandler(
            _client, _mediator, resolver, list, NullLogger<CardsBrowseCallbackHandler>.Instance);
    }

    private static CallbackQuery MakeCallback(string data) => new()
    {
        Id = "cb1",
        Data = data,
        From = new User { Id = TelegramId },
        Message = new Message { Id = 10, Chat = new Chat { Id = ChatId } },
    };

    [Fact]
    public async Task PageActionRequestsThatPage()
    {
        _mediator
            .Send(Arg.Any<ListCardsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<CardSummaryDto>>.Success(
                new PagedResult<CardSummaryDto>(Array.Empty<CardSummaryDto>(), 2, 5, 0)));

        await CreateSut().HandleAsync(MakeCallback("cards:page:2"), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<ListCardsQuery>(q => q.UserId == UserId && q.Page == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenActionFetchesTheCard()
    {
        _mediator
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CardDto>.Success(new CardDto(
                CardId, "Title", "Body", Array.Empty<string>(), Now, Now, CardType.Note)));

        var data = $"cards:open:3:{CardId.ToString("N", CultureInfo.InvariantCulture)}";
        await CreateSut().HandleAsync(MakeCallback(data), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<GetCardByIdQuery>(q => q.UserId == UserId && q.CardId == CardId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BadActionDoesNotQuery()
    {
        await CreateSut().HandleAsync(MakeCallback("cards:bogus"), CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<ListCardsQuery>(), Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>());
    }
}
