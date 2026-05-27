using System.Globalization;

using Memoria.Shared.Kernel.Observability;

using Serilog.Context;

using Telegram.Bot.Types;

namespace Memoria.Bot.Observability;

/// <summary>
/// Initializes the per-update <see cref="OperationContext"/> for the bot entry
/// point. Generates a fresh CorrelationId, extracts the originating Telegram
/// user id from either <see cref="Update.Message"/>, <see cref="Update.CallbackQuery"/>
/// or <see cref="Update.InlineQuery"/>, populates
/// <see cref="OperationContextAccessor"/>, and pushes properties into Serilog
/// <see cref="LogContext"/>. The returned <see cref="IDisposable"/> pops the
/// LogContext stack and clears the accessor — wrap the whole update handler
/// in a <c>using</c>.
/// </summary>
internal static class BotOperationScope
{
    private const string ModuleName = "Bot";

    public static IDisposable Enter(Update update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var fromId = update.Message?.From?.Id
                     ?? update.CallbackQuery?.From.Id
                     ?? update.InlineQuery?.From.Id;

        var ctx = new OperationContext
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            Module = ModuleName,
            TelegramUserId = fromId?.ToString(CultureInfo.InvariantCulture),
        };
        OperationContextAccessor.Current.Value = ctx;

        var corr = LogContext.PushProperty("CorrelationId", ctx.CorrelationId);
        var mod = LogContext.PushProperty("Module", ctx.Module);
        var tg = ctx.TelegramUserId is not null
            ? LogContext.PushProperty("TelegramUserId", ctx.TelegramUserId)
            : null;

        return new Scope(corr, mod, tg);
    }

    private sealed class Scope : IDisposable
    {
        private readonly IDisposable _corr;
        private readonly IDisposable _mod;
        private readonly IDisposable? _tg;

        public Scope(IDisposable corr, IDisposable mod, IDisposable? tg)
        {
            _corr = corr;
            _mod = mod;
            _tg = tg;
        }

        public void Dispose()
        {
            _tg?.Dispose();
            _mod.Dispose();
            _corr.Dispose();
            OperationContextAccessor.Current.Value = null;
        }
    }
}
