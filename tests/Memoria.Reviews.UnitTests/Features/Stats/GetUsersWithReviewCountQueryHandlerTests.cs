using FluentAssertions;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Domain;
using Memoria.Reviews.Features.Stats;
using Memoria.Reviews.UnitTests.Infrastructure;

namespace Memoria.Reviews.UnitTests.Features.Stats;

public sealed class GetUsersWithReviewCountQueryHandlerTests
{
    [Fact]
    public async Task HandleCountsDistinctUsers()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Reviews.AddRange(
            new Review(Guid.NewGuid(), u1, null, Rating.Good, "t", now, null),
            new Review(Guid.NewGuid(), u1, null, Rating.Easy, "t", now, null),
            new Review(Guid.NewGuid(), u2, null, Rating.Hard, "t", now, null));
        await db.SaveChangesAsync();

        var sut = new GetUsersWithReviewCountQueryHandler(db);
        var result = await sut.Handle(new GetUsersWithReviewCountQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
    }
}
