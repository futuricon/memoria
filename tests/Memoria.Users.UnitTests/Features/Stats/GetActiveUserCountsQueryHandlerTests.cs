using FluentAssertions;

using Memoria.Users.Contracts.Queries;
using Memoria.Users.Domain;
using Memoria.Users.Features.Stats;
using Memoria.Users.UnitTests.Infrastructure;

using Microsoft.Extensions.Time.Testing;

namespace Memoria.Users.UnitTests.Features.Stats;

public sealed class GetActiveUserCountsQueryHandlerTests
{
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task HandleBucketsByDayWeekMonthFromLastSeenAt()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var now = _clock.GetUtcNow().UtcDateTime;

        var u1 = new User("Day", "UTC", now); u1.BumpLastSeenAt(now.AddHours(-2));
        var u2 = new User("Week", "UTC", now); u2.BumpLastSeenAt(now.AddDays(-3));
        var u3 = new User("Month", "UTC", now); u3.BumpLastSeenAt(now.AddDays(-20));
        var u4 = new User("Old", "UTC", now); u4.BumpLastSeenAt(now.AddDays(-60));
        var u5 = new User("NeverSeen", "UTC", now);
        db.Users.AddRange(u1, u2, u3, u4, u5);
        await db.SaveChangesAsync();

        var sut = new GetActiveUserCountsQueryHandler(db, _clock);
        var result = await sut.Handle(new GetActiveUserCountsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Dau.Should().Be(1);
        result.Value.Wau.Should().Be(2);
        result.Value.Mau.Should().Be(3);
    }
}
