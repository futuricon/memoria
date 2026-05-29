using MediatR;

using Memoria.Bot.Localization;
using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Reminders.Contracts.Commands;

using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace Memoria.Bot.Callbacks;

/// <summary>
/// Handles <c>due:rev:&lt;reminderId&gt;</c> — enqueues immediate delivery of a
/// due reminder so the user can review it now. The reminder then arrives as a
/// normal review message (Note: show-answer; Question: type-your-answer).
/// </summary>
internal sealed class DueCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;
    private readonly ILogger<DueCallbackHandler> _logger;

    public DueCallbackHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver,
        ILogger<DueCallbackHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _mediator = mediator;
        _resolver = resolver;
        _logger = logger;
    }

    public string Prefix => "due:";

    public async Task HandleAsync(CallbackQuery callback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var parts = (callback.Data ?? string.Empty).Split(':');
        if (parts.Length < 3
            || !string.Equals(parts[1], "rev", StringComparison.Ordinal)
            || !Guid.TryParseExact(parts[2], "N", out var reminderId))
        {
            await Answer(callback, "Bad action", ct).ConfigureAwait(false);
            return;
        }

        var resolved = await _resolver.ResolveAsync(callback.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await Answer(callback, "Not linked", ct).ConfigureAwait(false);
            return;
        }

        var result = await _mediator.Send(
            new DeliverReminderNowCommand(reminderId, resolved.Value!.UserId), ct).ConfigureAwait(false);

        await Answer(callback, result.IsSuccess ? BotText.DueSending : result.Error!.Message, ct)
            .ConfigureAwait(false);
    }

    private async Task Answer(CallbackQuery callback, string? text, CancellationToken ct)
    {
        try
        {
            await _client.AnswerCallbackQuery(callback.Id, text, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to answer callback {CallbackId}", callback.Id);
        }
    }
}
