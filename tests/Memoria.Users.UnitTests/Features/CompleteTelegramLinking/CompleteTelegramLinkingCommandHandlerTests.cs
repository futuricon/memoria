using FluentAssertions;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Domain;
using Memoria.Users.Features.CompleteTelegramLinking;
using Memoria.Users.Persistence;
using Memoria.Users.UnitTests.Infrastructure;

namespace Memoria.Users.UnitTests.Features.CompleteTelegramLinking;

public sealed class CompleteTelegramLinkingCommandHandlerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero));
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private CompleteTelegramLinkingCommandHandler CreateSut(UsersDbContext db) =>
        new(db, _mediator, _clock);

    [Fact]
    public async Task HandleWithValidTokenLinksTelegramIdentityAndConsumesCode()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        var verification = new VerificationCode(
            userId: user.Id,
            purpose: VerificationPurpose.LinkTelegram,
            targetIdentifier: "tok-abc",
            codeHash: "hash-irrelevant",
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        db.VerificationCodes.Add(verification);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new CompleteTelegramLinkingCommand("tok-abc", "telegram-12345"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Merged.Should().BeFalse();
        (await db.Identities.AnyAsync(i =>
            i.UserId == user.Id
            && i.Provider == IdentityProvider.Telegram
            && i.ExternalId == "telegram-12345")).Should().BeTrue();

        var consumed = await db.VerificationCodes.FirstAsync(c => c.Id == verification.Id);
        consumed.ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleWithUnknownTokenReturnsNotFound()
    {
        await using var db = UsersDbContextTestFactory.Create();

        var result = await CreateSut(db).Handle(
            new CompleteTelegramLinkingCommand("does-not-exist", "telegram-1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleWithExpiredTokenReturnsValidation()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        var verification = new VerificationCode(
            userId: user.Id,
            purpose: VerificationPurpose.LinkTelegram,
            targetIdentifier: "tok-exp",
            codeHash: "hash",
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddMinutes(-1));
        db.VerificationCodes.Add(verification);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new CompleteTelegramLinkingCommand("tok-exp", "tg-1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task HandleWhenTelegramAlreadyLinkedToDifferentUserDispatchesMerge()
    {
        // Regression: this used to return Conflict; now it auto-merges the
        // existing Telegram-only account into the SPA-authenticated target.
        await using var db = UsersDbContextTestFactory.Create();
        var source = new User("Alice (Telegram-only)", "UTC", _clock.GetUtcNow().UtcDateTime);
        var target = new User("Alice (SPA)", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.AddRange(source, target);
        db.Identities.Add(new UserIdentity(
            source.Id, IdentityProvider.Telegram, "tg-shared", _clock.GetUtcNow().UtcDateTime));

        var verification = new VerificationCode(
            userId: target.Id,
            purpose: VerificationPurpose.LinkTelegram,
            targetIdentifier: "tok-merge",
            codeHash: "hash",
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        db.VerificationCodes.Add(verification);
        await db.SaveChangesAsync();

        var mergeStats = new MergeAccountsResultDto(CardsMoved: 5, RemindersMoved: 3, ReviewsMoved: 2);
        _mediator.Send(Arg.Any<MergeAccountsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<MergeAccountsResultDto>.Success(mergeStats));

        var result = await CreateSut(db).Handle(
            new CompleteTelegramLinkingCommand("tok-merge", "tg-shared"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Merged.Should().BeTrue();
        result.Value.MergeStats.Should().Be(mergeStats);

        await _mediator.Received(1).Send(
            Arg.Is<MergeAccountsCommand>(c =>
                c.SourceUserId == source.Id && c.TargetUserId == target.Id),
            Arg.Any<CancellationToken>());

        (await db.VerificationCodes.FirstAsync(c => c.Id == verification.Id))
            .ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleWhenTelegramAlreadyLinkedToSameUserIsIdempotent()
    {
        // Edge case: user re-tapped the deep-link after a successful link.
        // No merge dispatch, just consume the token.
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);
        db.Identities.Add(new UserIdentity(
            user.Id, IdentityProvider.Telegram, "tg-mine", _clock.GetUtcNow().UtcDateTime));

        var verification = new VerificationCode(
            userId: user.Id,
            purpose: VerificationPurpose.LinkTelegram,
            targetIdentifier: "tok-same",
            codeHash: "hash",
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        db.VerificationCodes.Add(verification);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new CompleteTelegramLinkingCommand("tok-same", "tg-mine"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Merged.Should().BeFalse();

        await _mediator.DidNotReceive().Send(
            Arg.Any<MergeAccountsCommand>(), Arg.Any<CancellationToken>());

        (await db.Identities.CountAsync(i => i.Provider == IdentityProvider.Telegram))
            .Should().Be(1, "no duplicate identity inserted");
    }
}
