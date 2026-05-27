namespace Memoria.Shared.Kernel.Observability;

/// <summary>
/// AsyncLocal-backed slot for the current <see cref="OperationContext"/>.
/// Use entry-point helpers (<c>OperationContextMiddleware</c> for API,
/// <c>BotOperationScope</c> for the bot) to populate this — direct mutation
/// outside those helpers is a code smell.
/// </summary>
public static class OperationContextAccessor
{
    public static readonly AsyncLocal<OperationContext?> Current = new();
}
