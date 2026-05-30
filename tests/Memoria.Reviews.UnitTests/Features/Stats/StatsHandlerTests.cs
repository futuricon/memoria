using FluentAssertions;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Domain;
using Memoria.Reviews.Features.Stats;
using Memoria.Reviews.UnitTests.Infrastructure;

using Microsoft.Extensions.Time.Testing;

namespace Memoria.Reviews.UnitTests.Features.Stats;

public sealed class StatsHandlerTests
{
    private static readonly DateTime Today = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Today, TimeSpan.Zero));

    private static Review NewReview(Guid userId, Guid cardId, Rating rating, DateTime when) =>
        new(cardId, userId, reminderId: null, rating, "title", when, note: null);

    // ===== Streak =====

    [Fact]
    public async Task StreakWhenNoReviewsReturnsZero()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var result = await new GetStreakQueryHandler(db, _clock).Handle(
            new GetStreakQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Current.Should().Be(0);
        result.Value.Longest.Should().Be(0);
        result.Value.LastReviewedOnUtc.Should().BeNull();
    }

    [Fact]
    public async Task StreakCountsConsecutiveDaysEndingToday()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        // Reviews on today, yesterday, day-before — streak of 3.
        db.Reviews.AddRange(
            NewReview(user, Guid.NewGuid(), Rating.Good, Today),
            NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddDays(-1)),
            NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddDays(-2)));
        await db.SaveChangesAsync();

        var result = await new GetStreakQueryHandler(db, _clock).Handle(
            new GetStreakQuery(user), CancellationToken.None);

        result.Value!.Current.Should().Be(3);
        result.Value.Longest.Should().Be(3);
    }

    [Fact]
    public async Task StreakAllowsGraceDayIfTodayHasNoReviewYet()
    {
        // No review today, but a streak ending yesterday — still alive.
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        db.Reviews.AddRange(
            NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddDays(-1)),
            NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddDays(-2)));
        await db.SaveChangesAsync();

        var result = await new GetStreakQueryHandler(db, _clock).Handle(
            new GetStreakQuery(user), CancellationToken.None);

        result.Value!.Current.Should().Be(2, "yesterday + day-before keep the streak alive without today");
    }

    [Fact]
    public async Task StreakIsBrokenIfYesterdayMissing()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        // Last review was 2 days ago — streak is dead.
        db.Reviews.Add(NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddDays(-2)));
        await db.SaveChangesAsync();

        var result = await new GetStreakQueryHandler(db, _clock).Handle(
            new GetStreakQuery(user), CancellationToken.None);

        result.Value!.Current.Should().Be(0);
        result.Value.Longest.Should().Be(1, "the single past day is the longest run");
    }

    [Fact]
    public async Task StreakLongestTracksHistoricalPeak()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        // 5-day run a month ago, then a gap, then today.
        for (var i = 0; i < 5; i++)
        {
            db.Reviews.Add(NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddDays(-30 - i)));
        }
        db.Reviews.Add(NewReview(user, Guid.NewGuid(), Rating.Good, Today));
        await db.SaveChangesAsync();

        var result = await new GetStreakQueryHandler(db, _clock).Handle(
            new GetStreakQuery(user), CancellationToken.None);

        result.Value!.Current.Should().Be(1);
        result.Value.Longest.Should().Be(5);
    }

    // ===== Rating distribution =====

    [Fact]
    public async Task RatingDistributionCountsByRatingWithinWindow()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        db.Reviews.AddRange(
            NewReview(user, Guid.NewGuid(), Rating.Forgot, Today.AddDays(-1)),
            NewReview(user, Guid.NewGuid(), Rating.Forgot, Today.AddDays(-2)),
            NewReview(user, Guid.NewGuid(), Rating.Hard, Today.AddDays(-3)),
            NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddDays(-4)),
            NewReview(user, Guid.NewGuid(), Rating.Easy, Today.AddDays(-5)),
            // Out of window — should not be counted.
            NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddDays(-100)));
        await db.SaveChangesAsync();

        var result = await new GetRatingDistributionQueryHandler(db, _clock).Handle(
            new GetRatingDistributionQuery(user, DaysBack: 30), CancellationToken.None);

        result.Value.Should().Be(new RatingDistributionDto(
            Forgot: 2, Hard: 1, Good: 1, Easy: 1));
        result.Value!.Total.Should().Be(5);
    }

    // ===== Activity heatmap =====

    [Fact]
    public async Task HeatmapGroupsByDateAndExcludesOutOfWindow()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        db.Reviews.AddRange(
            NewReview(user, Guid.NewGuid(), Rating.Good, Today),
            NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddHours(2)),
            NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddDays(-5)),
            // Out of window.
            NewReview(user, Guid.NewGuid(), Rating.Good, Today.AddDays(-200)));
        await db.SaveChangesAsync();

        var result = await new GetActivityHeatmapQueryHandler(db, _clock).Handle(
            new GetActivityHeatmapQuery(user, DaysBack: 90), CancellationToken.None);

        result.Value.Should().HaveCount(2);
        var todayBucket = result.Value!.First(d => d.DateUtc == DateOnly.FromDateTime(Today));
        todayBucket.Count.Should().Be(2);
    }

    // ===== Stuck cards =====

    [Fact]
    public async Task StuckCardsCardWithThreeForgotInARowIsReturned()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        var card = Guid.NewGuid();
        // Last three reviews (most recent first): Forgot, Forgot, Forgot. Older: Good.
        db.Reviews.AddRange(
            NewReview(user, card, Rating.Forgot, Today),
            NewReview(user, card, Rating.Forgot, Today.AddDays(-1)),
            NewReview(user, card, Rating.Forgot, Today.AddDays(-2)),
            NewReview(user, card, Rating.Good, Today.AddDays(-10)));
        await db.SaveChangesAsync();

        var result = await new GetStuckCardCandidatesQueryHandler(db).Handle(
            new GetStuckCardCandidatesQuery(user), CancellationToken.None);

        var single = result.Value!.Single();
        single.CardId.Should().Be(card);
        single.ConsecutiveForgotCount.Should().Be(3);
    }

    [Fact]
    public async Task StuckCardsCardWithGoodInLastNIsExcluded()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        var card = Guid.NewGuid();
        // Last 3 are NOT all Forgot — one of them is Good.
        db.Reviews.AddRange(
            NewReview(user, card, Rating.Forgot, Today),
            NewReview(user, card, Rating.Good, Today.AddDays(-1)),
            NewReview(user, card, Rating.Forgot, Today.AddDays(-2)));
        await db.SaveChangesAsync();

        var result = await new GetStuckCardCandidatesQueryHandler(db).Handle(
            new GetStuckCardCandidatesQuery(user), CancellationToken.None);

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task StuckCardsCardWithFewerThanThresholdIsExcluded()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        var card = Guid.NewGuid();
        db.Reviews.AddRange(
            NewReview(user, card, Rating.Forgot, Today),
            NewReview(user, card, Rating.Forgot, Today.AddDays(-1)));
        await db.SaveChangesAsync();

        var result = await new GetStuckCardCandidatesQueryHandler(db).Handle(
            new GetStuckCardCandidatesQuery(user, MinConsecutiveForgot: 3), CancellationToken.None);

        result.Value.Should().BeEmpty();
    }
}
