using FluentAssertions;

using Memoria.Users.Contracts.Queries;
using Memoria.Users.Domain;
using Memoria.Users.Features.Stats;
using Memoria.Users.UnitTests.Infrastructure;

namespace Memoria.Users.UnitTests.Features.Stats;

public sealed class GetSignupAndLinkCountsQueryHandlerTests
{
    [Fact]
    public async Task HandleCountsUsersAndDistinctTelegramLinkedUsers()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var now = DateTime.UtcNow;

        var u1 = new User("Alpha", "UTC", now);
        var u2 = new User("Beta", "UTC", now);
        var u3 = new User("Gamma", "UTC", now);
        db.Users.AddRange(u1, u2, u3);
        db.Identities.AddRange(
            new UserIdentity(u1.Id, IdentityProvider.Telegram, "tg-1", now),
            new UserIdentity(u2.Id, IdentityProvider.Telegram, "tg-2", now));
        await db.SaveChangesAsync();

        var sut = new GetSignupAndLinkCountsQueryHandler(db);
        var result = await sut.Handle(new GetSignupAndLinkCountsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalSignups.Should().Be(3);
        result.Value.TelegramLinked.Should().Be(2);
    }
}
