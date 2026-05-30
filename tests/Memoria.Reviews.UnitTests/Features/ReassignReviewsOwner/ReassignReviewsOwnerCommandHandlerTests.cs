using FluentAssertions;

using Memoria.Reviews.Contracts.Commands;
using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Domain;
using Memoria.Reviews.Features.ReassignReviewsOwner;
using Memoria.Reviews.UnitTests.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.UnitTests.Features.ReassignReviewsOwner;

public sealed class ReassignReviewsOwnerCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleMovesAllSourceReviewsToTarget()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        db.Reviews.AddRange(
            new Review(Guid.NewGuid(), source, reminderId: null, Rating.Good, "title", Now, note: null),
            new Review(Guid.NewGuid(), source, reminderId: null, Rating.Easy, "title", Now, note: null),
            new Review(Guid.NewGuid(), Guid.NewGuid(), reminderId: null, Rating.Hard, "title", Now, note: null));
        await db.SaveChangesAsync();

        var result = await new ReassignReviewsOwnerCommandHandler(db).Handle(
            new ReassignReviewsOwnerCommand(source, target), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        (await db.Reviews.CountAsync(r => r.UserId == source)).Should().Be(0);
        (await db.Reviews.CountAsync(r => r.UserId == target)).Should().Be(2);
    }

    [Fact]
    public async Task HandleIsIdempotentOnRerun()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        db.Reviews.Add(new Review(
            Guid.NewGuid(), source, null, Rating.Good, "t", Now, null));
        await db.SaveChangesAsync();

        var sut = new ReassignReviewsOwnerCommandHandler(db);
        await sut.Handle(new ReassignReviewsOwnerCommand(source, target), CancellationToken.None);
        var second = await sut.Handle(
            new ReassignReviewsOwnerCommand(source, target), CancellationToken.None);

        second.Value.Should().Be(0);
    }

    [Fact]
    public async Task HandleWhenSourceEqualsTargetReturnsZero()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var user = Guid.NewGuid();

        var result = await new ReassignReviewsOwnerCommandHandler(db).Handle(
            new ReassignReviewsOwnerCommand(user, user), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }
}
