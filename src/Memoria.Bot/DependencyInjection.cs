using Memoria.Bot.Adapters;
using Memoria.Bot.Callbacks;
using Memoria.Bot.Commands;
using Memoria.Bot.Conversations;
using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Reminders.Contracts.Abstractions;
using Memoria.Shared.Infrastructure.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Telegram.Bot;

namespace Memoria.Bot;

/// <summary>
/// Регистрирует Telegram-бота: <see cref="ITelegramBotClient"/>, long-polling
/// hosted service, центральный router, все command/callback-handler-ы,
/// FSM-store и адаптер <c>IReminderNotificationSender</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddBotModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpClient("telegram_bot")
            .AddTypedClient<ITelegramBotClient>((http, sp) =>
            {
                var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
                return new TelegramBotClient(options.BotToken, http);
            });

        services.AddSingleton<IConversationStateStore, InMemoryConversationStateStore>();
        services.AddSingleton<AddCardParser>();

        services.AddHostedService<TelegramBotHostedService>();

        services.AddScoped<IReminderNotificationSender, TelegramReminderNotificationSender>();

        services.AddScoped<BotMessageRouter>();
        services.AddScoped<CurrentUserResolver>();
        services.AddScoped<CardIdResolver>();
        services.AddScoped<ListCommandHandler>();
        services.AddScoped<AddCardDialogHandler>();

        services.AddScoped<ITextCommandHandler, HelpCommandHandler>();
        services.AddScoped<ITextCommandHandler, CancelCommandHandler>();
        services.AddScoped<ITextCommandHandler, StartCommandHandler>();
        services.AddScoped<ITextCommandHandler, MeCommandHandler>();
        services.AddScoped<ITextCommandHandler, TimezoneCommandHandler>();
        services.AddScoped<ITextCommandHandler, LoginCommandHandler>();
        services.AddScoped<ITextCommandHandler, TagsCommandHandler>();
        services.AddScoped<ITextCommandHandler>(sp => sp.GetRequiredService<ListCommandHandler>());
        services.AddScoped<ITextCommandHandler, CardCommandHandler>();
        services.AddScoped<ITextCommandHandler, DeleteCommandHandler>();
        services.AddScoped<ITextCommandHandler, AddCommandHandler>();

        services.AddScoped<ICallbackHandler, ListPaginationCallbackHandler>();
        services.AddScoped<ICallbackHandler, DeleteConfirmCallbackHandler>();
        services.AddScoped<ICallbackHandler, CardRestoreCallbackHandler>();
        services.AddScoped<ICallbackHandler, AddCardConfirmCallbackHandler>();
        services.AddScoped<ICallbackHandler, ReminderCallbackHandler>();

        services.AddScoped<IConversationContinuationHandler>(sp =>
            sp.GetRequiredService<AddCardDialogHandler>());

        return services;
    }
}
