using FluentAssertions;

using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;
using Memoria.Users.Domain;
using Memoria.Users.Features.Admin;
using Memoria.Users.UnitTests.Infrastructure;

namespace Memoria.Users.UnitTests.Features.Admin;

public sealed class GetUsersPageQueryHandlerTests
{
    private static User Make(string name, DateTime createdAt, string? email = null)
    {
        var u = new User(displayName: name, timeZoneId: "UTC", createdAt: createdAt);
        if (email is not null) u.SetEmail(email);
        return u;
    }

    [Fact]
    public async Task HandleReturnsFirstPageOrderedByCreatedAtDescByDefault()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Users.AddRange(
            Make("Oldest", t0),
            Make("Middle", t0.AddDays(1)),
            Make("Newest", t0.AddDays(2)));
        await db.SaveChangesAsync();

        var sut = new GetUsersPageQueryHandler(db);
        var result = await sut.Handle(
            new GetUsersPageQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(3);
        result.Value.Items.Select(i => i.DisplayName)
            .Should().ContainInOrder("Newest", "Middle", "Oldest");
    }

    [Fact]
    public async Task HandleAppliesPaginationAndClampsPageSize()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
        {
            db.Users.Add(Make($"User{i}", t0.AddDays(i)));
        }
        await db.SaveChangesAsync();

        var sut = new GetUsersPageQueryHandler(db);

        var page1 = (await sut.Handle(
            new GetUsersPageQuery(Page: 1, PageSize: 2, Search: null, Sort: UserSortKey.CreatedAtAsc),
            CancellationToken.None)).Value!;
        var page2 = (await sut.Handle(
            new GetUsersPageQuery(Page: 2, PageSize: 2, Search: null, Sort: UserSortKey.CreatedAtAsc),
            CancellationToken.None)).Value!;

        page1.Items.Should().HaveCount(2);
        page1.Items[0].DisplayName.Should().Be("User0");
        page2.Items[0].DisplayName.Should().Be("User2");
        page1.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task HandleIncludesSoftDeletedUsers()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var alive = Make("Alive", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var dead = Make("Dead", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        dead.GetType()
            .GetMethod("SoftDelete", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(dead, new object[] { new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc) });
        db.Users.AddRange(alive, dead);
        await db.SaveChangesAsync();

        var sut = new GetUsersPageQueryHandler(db);
        var result = await sut.Handle(
            new GetUsersPageQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            CancellationToken.None);

        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().Contain(i => i.DisplayName == "Dead" && i.DeletedAt != null);
    }

    [Fact]
    public async Task HandleSortByDisplayNameAlphabetical()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Users.AddRange(
            Make("Charlie", t0),
            Make("Alice", t0),
            Make("Bob", t0));
        await db.SaveChangesAsync();

        var sut = new GetUsersPageQueryHandler(db);
        var result = await sut.Handle(
            new GetUsersPageQuery(Page: 1, PageSize: 10, Search: null, Sort: UserSortKey.DisplayNameAsc),
            CancellationToken.None);

        result.Value!.Items.Select(i => i.DisplayName)
            .Should().ContainInOrder("Alice", "Bob", "Charlie");
    }

    [Fact]
    public async Task HandleProjectsAllFieldsIncludingRoleAndIsBlocked()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var u = Make("Admin", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "admin@example.com");
        u.PromoteToAdmin();
        u.Block();
        u.BumpLastSeenAt(new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc));
        db.Users.Add(u);
        await db.SaveChangesAsync();

        var sut = new GetUsersPageQueryHandler(db);
        var result = await sut.Handle(
            new GetUsersPageQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            CancellationToken.None);

        var row = result.Value!.Items.Single();
        row.DisplayName.Should().Be("Admin");
        row.Email.Should().Be("admin@example.com");
        row.Role.Should().Be(Role.Admin);
        row.IsBlocked.Should().BeTrue();
        row.LastSeenAt.Should().Be(new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc));
        row.DeletedAt.Should().BeNull();
    }
}
