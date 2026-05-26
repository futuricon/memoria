using FluentAssertions;

using Memoria.Bot.Services;

namespace Memoria.Bot.UnitTests.Services;

public sealed class AddCardParserTests
{
    private readonly AddCardParser _sut = new();

    [Fact]
    public void ParseSimpleTitleAndBody()
    {
        var result = _sut.Parse("Title\n\nBody");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Title");
        result.Value.Tags.Should().BeEmpty();
        result.Value.Body.Should().Be("Body");
    }

    [Fact]
    public void ParseTagsOnTitleLine()
    {
        var result = _sut.Parse("Title #postgres\nBody text");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Title");
        result.Value.Tags.Should().BeEquivalentTo(new[] { "postgres" });
        result.Value.Body.Should().Be("Body text");
    }

    [Fact]
    public void ParseTagsOnSeparateLine()
    {
        var result = _sut.Parse("PostgreSQL VACUUM\n#postgres #database\n\nVACUUM cleans dead tuples...");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("PostgreSQL VACUUM");
        result.Value.Tags.Should().BeEquivalentTo(new[] { "postgres", "database" });
        result.Value.Body.Should().Be("VACUUM cleans dead tuples...");
    }

    [Fact]
    public void ParseMultipleTagLines()
    {
        var result = _sut.Parse("Title\n#a #b\nbody1\n#c\nbody2");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Tags.Should().BeEquivalentTo(new[] { "a", "b", "c" });
        result.Value.Body.Should().Be("body1\n\nbody2");
    }

    [Fact]
    public void ParseEmptyTitleReturnsValidationError()
    {
        var result = _sut.Parse("   \nBody");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("bot.add_title_empty");
    }

    [Fact]
    public void ParseEmptyBodyReturnsValidationError()
    {
        var result = _sut.Parse("Title only");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("bot.add_body_empty");
    }
}
