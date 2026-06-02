using FluentAssertions;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Domain;
using Memoria.Reviews.Features.Stats;
using Memoria.Reviews.UnitTests.Infrastructure;

using Microsoft.Extensions.Time.Testing;

namespace Memoria.Reviews.UnitTests.Features.Stats;

public sealed class GetAiCalibrationQueryHandlerTests
{
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private static Review Graded(Guid userId, Rating rating, int score, DateTime when) =>
        new(cardId: Guid.NewGuid(),
            userId: userId,
            reminderId: null,
            rating: rating,
            cardTitleSnapshot: "t",
            reviewedAt: when,
            note: null,
            answerText: "x",
            aiScore: score,
            aiFeedback: null,
            autoGraded: true);

    [Fact]
    public async Task HandleBucketsByScoreAndCountsRatings()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        var now = _clock.GetUtcNow().UtcDateTime;

        db.Reviews.AddRange(
            // bucket 0-20: 1 Forgot
            Graded(user, Rating.Forgot, 10, now),
            // bucket 40-60: 1 Hard, 1 Good
            Graded(user, Rating.Hard, 45, now),
            Graded(user, Rating.Good, 55, now),
            // bucket 80-101: 2 Easy
            Graded(user, Rating.Easy, 90, now),
            Graded(user, Rating.Easy, 100, now),
            // non-graded review (no AI score) — must be skipped
            new Review(Guid.NewGuid(), user, null, Rating.Good, "t", now, null));
        await db.SaveChangesAsync();

        var sut = new GetAiCalibrationQueryHandler(db, _clock);
        var result = await sut.Handle(new GetAiCalibrationQuery(90), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var buckets = result.Value!;
        buckets.Should().HaveCount(5);

        buckets[0].Forgot.Should().Be(1);
        buckets[0].Total.Should().Be(1);

        buckets[2].Hard.Should().Be(1);
        buckets[2].Good.Should().Be(1);
        buckets[2].Total.Should().Be(2);

        buckets[4].Easy.Should().Be(2);
        buckets[4].Total.Should().Be(2);

        // Empty buckets exist
        buckets[1].Total.Should().Be(0);
        buckets[3].Total.Should().Be(0);
    }
}
