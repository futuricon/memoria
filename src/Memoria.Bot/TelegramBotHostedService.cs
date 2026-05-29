using Memoria.Bot.Observability;
using Memoria.Bot.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Memoria.Bot;

internal sealed class TelegramBotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramBotHostedService> _logger;

    public TelegramBotHostedService(
        ITelegramBotClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramBotHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
            DropPendingUpdates = false,
        };

        await RegisterCommandMenuAsync(stoppingToken).ConfigureAwait(false);

        _logger.LogInformation("Starting Telegram long polling…");

        await _client.ReceiveAsync(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken).ConfigureAwait(false);
    }

    // Populates the Telegram "Menu" button + the `/` autocomplete list. Failure
    // here must not stop the bot — the commands still work without the menu.
    private async Task RegisterCommandMenuAsync(CancellationToken ct)
    {
        BotCommand[] commands =
        [
            new() { Command = "menu", Description = "🏠 Open the menu" },
            new() { Command = "add", Description = "➕ Create a new card" },
            new() { Command = "due", Description = "⏰ Cards to review now" },
            new() { Command = "list", Description = "📋 Show your cards" },
            new() { Command = "card", Description = "🔍 View a card by id" },
            new() { Command = "delete", Description = "🗑 Delete a card by id" },
            new() { Command = "tags", Description = "🏷 List your tags" },
            new() { Command = "timezone", Description = "🕘 Set your timezone" },
            new() { Command = "me", Description = "👤 Your profile" },
            new() { Command = "login", Description = "🔑 Sign in to the web app" },
            new() { Command = "help", Description = "❓ Show help" },
            new() { Command = "cancel", Description = "✖️ Cancel the current action" },
        ];

        try
        {
            await _client.SetMyCommands(commands, cancellationToken: ct).ConfigureAwait(false);
            _logger.LogInformation("Registered {Count} bot commands with Telegram", commands.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to register bot command menu (commands still work)");
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        using var operationScope = BotOperationScope.Enter(update);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<BotMessageRouter>();
        try
        {
            await router.RouteAsync(update, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unhandled exception while routing update {UpdateId}", update.Id);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient client, Exception ex, CancellationToken ct)
    {
        _logger.LogWarning(ex, "Telegram polling error (auto-retried by client)");
        return Task.CompletedTask;
    }
}
