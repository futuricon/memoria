namespace Memoria.AI.Options;

/// <summary>
/// Per-model billing rate for the spend dashboard. Values are list price in
/// USD per 1 000 000 tokens. Internally we always pay-as-you-go on input +
/// output separately, so they're stored as two numbers.
/// </summary>
public sealed class AiModelPriceOptions
{
    public decimal InputUsdPerMillion { get; init; }
    public decimal OutputUsdPerMillion { get; init; }
}
