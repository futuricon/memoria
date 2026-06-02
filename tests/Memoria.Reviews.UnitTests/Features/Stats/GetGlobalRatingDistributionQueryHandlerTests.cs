using FluentAssertions;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Domain;
using Memoria.Reviews.Features.Stats;
using Memoria.Reviews.UnitTests.Infrastructure;

using Microsoft.Extensions.Time.Testing;

namespace Memoria.Reviews.UnitTests.Features.Stats;

public sealed class GetGlobalRatingDistributionQueryHandlerTests
{
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task HandleAggregatesAcrossAllUsersInWindow()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var now = _clock.GetUtcNow().UtcDateTime;
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        db.Reviews.AddRange(
            new Review(Guid.NewGuid(), a, null, Rating.Good, "t", now, null),
            new Review(Guid.NewGuid(), a, null, Rating.Hard, "t", now.AddDays(-2), null),
            new Review(Guid.NewGuid(), b, null, Rating.Good, "t", now.AddDays(-5), null),
            new Review(Guid.NewGuid(), b, null, Rating.Forgot, "t", now.AddDays(-10), null),
            // outside 30-day window
            new Review(Guid.NewGuid(), a, null, Rating.Easy, "t", now.AddDays(-40), null));
        await db.SaveChangesAsync();

        var sut = new GetGlobalRatingDistributionQueryHandler(db, _clock);
        var result = await sut.Handle(new GetGlobalRatingDistributionQuery(30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Forgot.Should().Be(1);
        result.Value.Hard.Should().Be(1);
        result.Value.Good.Should().Be(2);
        result.Value.Easy.Should().Be(0);
        result.Value.Total.Should().Be(4);
    }
}
