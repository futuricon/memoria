using FluentAssertions;

using Memoria.Cards.Domain;

namespace Memoria.Cards.UnitTests.Domain;

public sealed class CardPauseTests
{
    private static readonly DateTime ClockUtc = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    private static Card NewCard() =>
        new(Guid.NewGuid(), "Title", "Body", ClockUtc);

    [Fact]
    public void PauseFromActiveSetsIsPausedAndStoresStage()
    {
        var sut = NewCard();

        sut.Pause(stage: 3, ClockUtc);

        sut.IsPaused.Should().BeTrue();
        sut.PausedAtStage.Should().Be(3);
        sut.UpdatedAt.Should().Be(ClockUtc);
    }

    [Fact]
    public void PauseWithNullStageIsAllowed()
    {
        var sut = NewCard();

        sut.Pause(stage: null, ClockUtc);

        sut.IsPaused.Should().BeTrue();
        sut.PausedAtStage.Should().BeNull();
    }

    [Fact]
    public void PauseWhenAlreadyPausedThrows()
    {
        var sut = NewCard();
        sut.Pause(stage: 2, ClockUtc);

        Action act = () => sut.Pause(stage: 5, ClockUtc);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UnpauseClearsFlagsAndReturnsStoredStage()
    {
        var sut = NewCard();
        sut.Pause(stage: 4, ClockUtc);

        var stage = sut.Unpause(ClockUtc);

        stage.Should().Be(4);
        sut.IsPaused.Should().BeFalse();
        sut.PausedAtStage.Should().BeNull("the stored stage is cleared after the caller consumes it");
    }

    [Fact]
    public void UnpauseWhenNotPausedThrows()
    {
        var sut = NewCard();

        Action act = () => sut.Unpause(ClockUtc);

        act.Should().Throw<InvalidOperationException>();
    }
}
