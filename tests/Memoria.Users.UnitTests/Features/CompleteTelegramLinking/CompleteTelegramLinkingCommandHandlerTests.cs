using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Domain;
using Memoria.Users.Features.CompleteTelegramLinking;
using Memoria.Users.UnitTests.Infrastructure;

namespace Memoria.Users.UnitTests.Features.CompleteTelegramLinking;

public sealed class CompleteTelegramLinkingCommandHandlerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero));

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

        var sut = new CompleteTelegramLinkingCommandHandler(db, _clock);
        var result = await sut.Handle(
            new CompleteTelegramLinkingCommand("tok-abc", "telegram-12345"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
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
        var sut = new CompleteTelegramLinkingCommandHandler(db, _clock);

        var result = await sut.Handle(
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

        var sut = new CompleteTelegramLinkingCommandHandler(db, _clock);
        var result = await sut.Handle(
            new CompleteTelegramLinkingCommand("tok-exp", "tg-1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task HandleWhenTelegramAlreadyLinkedReturnsConflict()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user1 = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        var user2 = new User("Bob", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.AddRange(user1, user2);

        db.Identities.Add(new UserIdentity(user1.Id, IdentityProvider.Telegram, "tg-shared", _clock.GetUtcNow().UtcDateTime));

        var verification = new VerificationCode(
            userId: user2.Id,
            purpose: VerificationPurpose.LinkTelegram,
            targetIdentifier: "tok-2",
            codeHash: "hash",
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        db.VerificationCodes.Add(verification);
        await db.SaveChangesAsync();

        var sut = new CompleteTelegramLinkingCommandHandler(db, _clock);
        var result = await sut.Handle(
            new CompleteTelegramLinkingCommand("tok-2", "tg-shared"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }
}
