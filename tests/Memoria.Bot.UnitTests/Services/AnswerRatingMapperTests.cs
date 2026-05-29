using FluentAssertions;

using Memoria.Bot.Services;
using Memoria.Reviews.Contracts.Dtos;

namespace Memoria.Bot.UnitTests.Services;

public sealed class AnswerRatingMapperTests
{
    [Theory]
    [InlineData(100, Rating.Easy)]
    [InlineData(85, Rating.Easy)]
    [InlineData(84, Rating.Good)]
    [InlineData(65, Rating.Good)]
    [InlineData(64, Rating.Hard)]
    [InlineData(40, Rating.Hard)]
    [InlineData(39, Rating.Forgot)]
    [InlineData(0, Rating.Forgot)]
    public void FromScoreMapsToExpectedRating(int score, Rating expected)
    {
        AnswerRatingMapper.FromScore(score).Should().Be(expected);
    }
}
