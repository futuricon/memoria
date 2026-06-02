using FluentAssertions;

using Memoria.Users.Contracts.Queries;
using Memoria.Users.Domain;
using Memoria.Users.Features.Stats;
using Memoria.Users.UnitTests.Infrastructure;

using Microsoft.Extensions.Time.Testing;

namespace Memoria.Users.UnitTests.Features.Stats;

public sealed class GetRetentionCohortsQueryHandlerTests
{
    // Cohort window ends 30 days ago — so users created between 60 days
    // ago and 30 days ago are in the cohort.
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task HandleCountsRetainedByLastSeenDelta()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var now = _clock.GetUtcNow().UtcDateTime;
        var inCohort = now.AddDays(-45);

        var u1 = new User("D30", "UTC", inCohort);
        u1.BumpLastSeenAt(inCohort.AddDays(31)); // d1 + d7 + d30

        var u2 = new User("D7", "UTC", inCohort);
        u2.BumpLastSeenAt(inCohort.AddDays(8)); // d1 + d7

        var u3 = new User("D1", "UTC", inCohort);
        u3.BumpLastSeenAt(inCohort.AddDays(2)); // d1

        var u4 = new User("Bounced", "UTC", inCohort);
        // never seen again → no retention

        var u5 = new User("Recent", "UTC", now.AddDays(-5)); // outside cohort (too new)

        db.Users.AddRange(u1, u2, u3, u4, u5);
        await db.SaveChangesAsync();

        var sut = new GetRetentionCohortsQueryHandler(db, _clock);
        var result = await sut.Handle(new GetRetentionCohortsQuery(30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Signups.Should().Be(4);
        result.Value.D1Retained.Should().Be(3);
        result.Value.D7Retained.Should().Be(2);
        result.Value.D30Retained.Should().Be(1);
    }
}
