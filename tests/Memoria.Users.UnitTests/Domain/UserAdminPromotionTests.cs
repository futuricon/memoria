using FluentAssertions;

using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Domain;

namespace Memoria.Users.UnitTests.Domain;

public sealed class UserAdminPromotionTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkTokenIssuedBumpsLastSeenAtAlways()
    {
        var user = new User("Alice", "UTC", Now.AddDays(-7));

        user.MarkTokenIssued(Now, adminEmails: Array.Empty<string>());

        user.LastSeenAt.Should().Be(Now);
        user.Role.Should().Be(Role.User);
    }

    [Fact]
    public void MarkTokenIssuedPromotesWhenEmailInList()
    {
        var user = new User("Alice", "UTC", Now);
        user.SetEmail("admin@memoria.io");

        user.MarkTokenIssued(Now, adminEmails: new[] { "other@x.com", "admin@memoria.io" });

        user.Role.Should().Be(Role.Admin);
    }

    [Fact]
    public void MarkTokenIssuedPromotionIsCaseInsensitive()
    {
        var user = new User("Alice", "UTC", Now);
        user.SetEmail("admin@MEMORIA.io");

        user.MarkTokenIssued(Now, adminEmails: new[] { "Admin@memoria.IO" });

        user.Role.Should().Be(Role.Admin);
    }

    [Fact]
    public void MarkTokenIssuedDoesNotPromoteWhenEmailNotInList()
    {
        var user = new User("Bob", "UTC", Now);
        user.SetEmail("bob@x.com");

        user.MarkTokenIssued(Now, adminEmails: new[] { "admin@memoria.io" });

        user.Role.Should().Be(Role.User);
    }

    [Fact]
    public void MarkTokenIssuedDoesNotPromoteWhenEmailIsNull()
    {
        var user = new User("Telegram-only", "UTC", Now);

        user.MarkTokenIssued(Now, adminEmails: new[] { "admin@memoria.io" });

        user.Role.Should().Be(Role.User);
    }

    [Fact]
    public void MarkTokenIssuedIsIdempotentForExistingAdmin()
    {
        var user = new User("Alice", "UTC", Now);
        user.SetEmail("admin@memoria.io");
        user.PromoteToAdmin();

        user.MarkTokenIssued(Now, adminEmails: Array.Empty<string>());

        user.Role.Should().Be(Role.Admin); // stays admin even though no longer in list
    }
}
