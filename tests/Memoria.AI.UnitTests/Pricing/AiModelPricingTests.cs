using FluentAssertions;

using Memoria.AI.Options;
using Memoria.AI.Pricing;

using Microsoft.Extensions.Options;

namespace Memoria.AI.UnitTests.Pricing;

public sealed class AiModelPricingTests
{
    private static AiModelPricing CreateSut(AiOptions options)
    {
        var monitor = new TestOptionsMonitor<AiOptions>(options);
        return new AiModelPricing(monitor);
    }

    [Fact]
    public void EstimateKnownModelMultipliesRates()
    {
        var sut = CreateSut(new AiOptions
        {
            Pricing = new(StringComparer.OrdinalIgnoreCase)
            {
                ["claude-sonnet-4-6"] = new()
                {
                    InputUsdPerMillion = 3.00m,
                    OutputUsdPerMillion = 15.00m,
                },
            },
        });

        // 1 000 000 input @ $3 + 500 000 output @ $15 = $3 + $7.50 = $10.50
        var cost = sut.Estimate("claude-sonnet-4-6", 1_000_000, 500_000);

        cost.Should().Be(10.50m);
    }

    [Fact]
    public void EstimateUnknownModelReturnsZero()
    {
        var sut = CreateSut(new AiOptions());

        sut.Estimate("not-in-table", 1_000, 1_000).Should().Be(0m);
    }

    [Fact]
    public void EstimateEmptyModelReturnsZero()
    {
        var sut = CreateSut(new AiOptions());

        sut.Estimate(string.Empty, 1_000, 1_000).Should().Be(0m);
    }

    [Fact]
    public void EstimateIsCaseInsensitiveOnModelName()
    {
        var sut = CreateSut(new AiOptions
        {
            Pricing = new(StringComparer.OrdinalIgnoreCase)
            {
                ["DeepSeek-Chat"] = new()
                {
                    InputUsdPerMillion = 0.27m,
                    OutputUsdPerMillion = 1.10m,
                },
            },
        });

        sut.Estimate("deepseek-chat", 1_000_000, 0).Should().Be(0.27m);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
