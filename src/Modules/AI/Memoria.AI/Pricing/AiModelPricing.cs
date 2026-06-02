using Memoria.AI.Options;

using Microsoft.Extensions.Options;

namespace Memoria.AI.Pricing;

/// <summary>
/// Converts (model, input tokens, output tokens) into an estimated USD cost
/// for the admin dashboard. Reads rates from <see cref="AiOptions.Pricing"/>;
/// an unknown model contributes 0 cost rather than throwing — pricing drift
/// is a finance concern, not a request-path one.
/// </summary>
internal sealed class AiModelPricing
{
    private readonly IOptionsMonitor<AiOptions> _options;

    public AiModelPricing(IOptionsMonitor<AiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public decimal Estimate(string model, int inputTokens, int outputTokens)
    {
        if (string.IsNullOrWhiteSpace(model)) return 0m;

        var pricing = _options.CurrentValue.Pricing;
        if (!pricing.TryGetValue(model, out var rate)) return 0m;

        return inputTokens / 1_000_000m * rate.InputUsdPerMillion
            + outputTokens / 1_000_000m * rate.OutputUsdPerMillion;
    }
}
