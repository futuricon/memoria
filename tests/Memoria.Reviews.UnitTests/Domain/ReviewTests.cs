using FluentAssertions;

using Memoria.Reviews.Domain;

namespace Memoria.Reviews.UnitTests.Domain;

public sealed class ReviewTests
{
    private static readonly DateTime SampleReviewedAt = new(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CtorAssignsAllFields()
    {
        var cardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();

        var review = new Review(
            cardId,
            userId,
            reminderId,
            Rating.Good,
            cardTitleSnapshot: "PostgreSQL VACUUM",
            reviewedAt: SampleReviewedAt,
            note: "remembered");

        review.CardId.Should().Be(cardId);
        review.UserId.Should().Be(userId);
        review.ReminderId.Should().Be(reminderId);
        review.Rating.Should().Be(Rating.Good);
        review.CardTitleSnapshot.Should().Be("PostgreSQL VACUUM");
        review.ReviewedAt.Should().Be(SampleReviewedAt);
        review.Note.Should().Be("remembered");
    }

    [Fact]
    public void CtorGeneratesNonEmptyId()
    {
        var review = new Review(
            Guid.NewGuid(), Guid.NewGuid(), null, Rating.Easy, "Title", SampleReviewedAt, null);

        review.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CtorAcceptsNullReminderId()
    {
        var review = new Review(
            Guid.NewGuid(), Guid.NewGuid(), reminderId: null, Rating.Hard, "Title", SampleReviewedAt, "manual");

        review.ReminderId.Should().BeNull();
    }

    [Fact]
    public void CtorAcceptsNullNote()
    {
        var review = new Review(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Rating.Forgot, "Title", SampleReviewedAt, note: null);

        review.Note.Should().BeNull();
    }
}
