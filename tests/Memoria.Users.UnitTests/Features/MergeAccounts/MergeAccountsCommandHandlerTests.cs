using FluentAssertions;

using MediatR;

using Memoria.Cards.Contracts.Commands;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reviews.Contracts.Commands;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Domain;
using Memoria.Users.Features.MergeAccounts;
using Memoria.Users.Persistence;
using Memoria.Users.UnitTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Users.UnitTests.Features.MergeAccounts;

public sealed class MergeAccountsCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Now, TimeSpan.Zero));
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    public MergeAccountsCommandHandlerTests()
    {
        StubCrossModule(cards: 0, reminders: 0, reviews: 0);
    }

    private void StubCrossModule(int cards, int reminders, int reviews)
    {
        _mediator.Send(Arg.Any<CancelRemindersForUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(Unit.Value));
        _mediator.Send(Arg.Any<ReassignCardsOwnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(cards));
        _mediator.Send(Arg.Any<ReassignRemindersOwnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(reminders));
        _mediator.Send(Arg.Any<ReassignReviewsOwnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(reviews));
    }

    private MergeAccountsCommandHandler CreateSut(UsersDbContext db) =>
        new(db, _mediator, _clock, NullLogger<MergeAccountsCommandHandler>.Instance);

    [Fact]
    public async Task HandleHappyPathReturnsCountsAndSoftDeletesSource()
    {
        StubCrossModule(cards: 5, reminders: 3, reviews: 7);

        await using var db = UsersDbContextTestFactory.Create();
        var source = new User("Bot account", "UTC", Now);
        var target = new User("SPA account", "UTC", Now);
        db.Users.AddRange(source, target);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new MergeAccountsCommand(source.Id, target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CardsMoved.Should().Be(5);
        result.Value.RemindersMoved.Should().Be(3);
        result.Value.ReviewsMoved.Should().Be(7);

        var sourceAfter = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == source.Id);
        sourceAfter.DeletedAt.Should().NotBeNull();

        // Source no longer visible under regular query filter.
        (await db.Users.AnyAsync(u => u.Id == source.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task HandleClearsSourceEmailAndPurgesTokens()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var source = new User("S", "UTC", Now);
        source.SetEmail("source@example.com");
        var target = new User("T", "UTC", Now);
        db.Users.AddRange(source, target);
        db.RefreshTokens.Add(new RefreshToken(source.Id, "hash-1", Now.AddDays(30), Now));
        db.RefreshTokens.Add(new RefreshToken(source.Id, "hash-2", Now.AddDays(30), Now));
        db.RefreshTokens.Add(new RefreshToken(target.Id, "hash-3", Now.AddDays(30), Now));
        db.VerificationCodes.Add(new VerificationCode(
            source.Id, VerificationPurpose.LinkEmail, "source@example.com", "h", Now.AddMinutes(5)));
        await db.SaveChangesAsync();

        await CreateSut(db).Handle(
            new MergeAccountsCommand(source.Id, target.Id), CancellationToken.None);

        var sourceAfter = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == source.Id);
        sourceAfter.Email.Should().BeNull("frees the unique index slot");
        (await db.RefreshTokens.CountAsync(t => t.UserId == source.Id)).Should().Be(0);
        (await db.RefreshTokens.CountAsync(t => t.UserId == target.Id)).Should().Be(1);
        (await db.VerificationCodes.CountAsync(c => c.UserId == source.Id)).Should().Be(0);
    }

    [Fact]
    public async Task HandleIdentityCollisionDeletesSourceRow()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var source = new User("S", "UTC", Now);
        var target = new User("T", "UTC", Now);
        db.Users.AddRange(source, target);
        // Same Google sub on both — should be deduped.
        db.Identities.Add(new UserIdentity(source.Id, IdentityProvider.Google, "g-abc", Now));
        db.Identities.Add(new UserIdentity(target.Id, IdentityProvider.Google, "g-abc", Now));
        await db.SaveChangesAsync();

        await CreateSut(db).Handle(
            new MergeAccountsCommand(source.Id, target.Id), CancellationToken.None);

        var googleIdentities = await db.Identities
            .Where(i => i.Provider == IdentityProvider.Google && i.ExternalId == "g-abc")
            .ToListAsync();
        googleIdentities.Should().HaveCount(1, "duplicate collapsed");
        googleIdentities.Single().UserId.Should().Be(target.Id);
    }

    [Fact]
    public async Task HandleIdentityNoCollisionRepointsToTarget()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var source = new User("S", "UTC", Now);
        var target = new User("T", "UTC", Now);
        db.Users.AddRange(source, target);
        // Telegram on source, no Telegram on target — should hand the identity over.
        db.Identities.Add(new UserIdentity(source.Id, IdentityProvider.Telegram, "tg-123", Now));
        await db.SaveChangesAsync();

        await CreateSut(db).Handle(
            new MergeAccountsCommand(source.Id, target.Id), CancellationToken.None);

        var tg = await db.Identities.SingleAsync(i => i.Provider == IdentityProvider.Telegram);
        tg.UserId.Should().Be(target.Id);
    }

    [Fact]
    public async Task HandleWhenSourceEqualsTargetReturnsValidation()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("X", "UTC", Now);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new MergeAccountsCommand(user.Id, user.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task HandleWhenSourceMissingReturnsNotFound()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var target = new User("T", "UTC", Now);
        db.Users.Add(target);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new MergeAccountsCommand(Guid.NewGuid(), target.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleWhenTargetMissingReturnsNotFound()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var source = new User("S", "UTC", Now);
        db.Users.Add(source);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new MergeAccountsCommand(source.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleDispatchesAllFourCrossModuleCommandsInOrder()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var source = new User("S", "UTC", Now);
        var target = new User("T", "UTC", Now);
        db.Users.AddRange(source, target);
        await db.SaveChangesAsync();

        await CreateSut(db).Handle(
            new MergeAccountsCommand(source.Id, target.Id), CancellationToken.None);

        Received.InOrder(() =>
        {
            _mediator.Send(Arg.Is<CancelRemindersForUserCommand>(c => c.UserId == source.Id), Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Is<ReassignCardsOwnerCommand>(c => c.SourceUserId == source.Id && c.TargetUserId == target.Id), Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Is<ReassignRemindersOwnerCommand>(c => c.SourceUserId == source.Id && c.TargetUserId == target.Id), Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Is<ReassignReviewsOwnerCommand>(c => c.SourceUserId == source.Id && c.TargetUserId == target.Id), Arg.Any<CancellationToken>());
        });
    }
}
