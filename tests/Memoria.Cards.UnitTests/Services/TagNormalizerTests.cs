using System.Diagnostics.CodeAnalysis;
using FluentAssertions;

using Memoria.Cards.Services;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.UnitTests.Services;

[SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments")]
public sealed class TagNormalizerTests
{
    private readonly TagNormalizer _sut = new();

    [Theory]
    [InlineData("postgres", "postgres")]
    [InlineData("  postgres  ", "postgres")]
    [InlineData("PostgreSQL", "postgresql")]
    [InlineData("My Tag", "my-tag")]
    [InlineData("multi   word  tag", "multi-word-tag")]
    [InlineData("dash-already-here", "dash-already-here")]
    [InlineData("MIXED Case Tag", "mixed-case-tag")]
    public void NormalizeProducesExpectedString(string input, string expected)
    {
        var result = _sut.Normalize(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeWithEmptyOrWhitespaceReturnsValidation(string input)
    {
        var result = _sut.Normalize(input);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("cards.tag_empty");
    }

    [Theory]
    [InlineData("tag#with-hash")]
    [InlineData("tag/slash")]
    [InlineData("кириллица")]
    [InlineData("emoji🎯here")]
    public void NormalizeWithInvalidCharsReturnsValidation(string input)
    {
        var result = _sut.Normalize(input);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("cards.tag_invalid_chars");
    }

    [Fact]
    public void NormalizeWithSingleCharReturnsTooShort()
    {
        var result = _sut.Normalize("a");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("cards.tag_too_short");
    }

    [Fact]
    public void NormalizeWithVeryLongTagReturnsTooLong()
    {
        var input = new string('a', 31);

        var result = _sut.Normalize(input);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("cards.tag_too_long");
    }

    [Fact]
    public void NormalizeManyDedupesDifferentCasings()
    {
        var raw = new[] { "PostgreSQL", "postgres", "postgresql", "Postgres" };

        var result = _sut.NormalizeMany(raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { "postgresql", "postgres" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void NormalizeManyDedupesEqualNamesAfterNormalization()
    {
        var raw = new[] { "my tag", "MY TAG", "  my tag  " };

        var result = _sut.NormalizeMany(raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { "my-tag" });
    }

    [Fact]
    public void NormalizeManyFailsFastOnFirstInvalidTag()
    {
        var raw = new[] { "valid", "bad#tag", "another" };

        var result = _sut.NormalizeMany(raw);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("cards.tag_invalid_chars");
    }

    [Fact]
    public void NormalizeManyEmptyInputReturnsEmptyList()
    {
        var result = _sut.NormalizeMany(Array.Empty<string>());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
