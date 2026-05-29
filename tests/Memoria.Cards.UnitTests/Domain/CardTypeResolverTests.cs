using FluentAssertions;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Domain;

namespace Memoria.Cards.UnitTests.Domain;

public sealed class CardTypeResolverTests
{
    [Theory]
    [InlineData("What is VACUUM?")]
    [InlineData("Is a clustered index sorted?")]
    [InlineData("Réponse à la question?")]
    [InlineData("Trailing whitespace still counts?   ")]
    [InlineData("?")]
    public void FromTitleWithTrailingQuestionMarkReturnsQuestion(string title)
    {
        CardTypeResolver.FromTitle(title).Should().Be(CardType.Question);
    }

    [Theory]
    [InlineData("PostgreSQL VACUUM")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Why? Because indexes.")]
    [InlineData("A note about MVCC.")]
    public void FromTitleWithoutTrailingQuestionMarkReturnsNote(string title)
    {
        CardTypeResolver.FromTitle(title).Should().Be(CardType.Note);
    }
}
