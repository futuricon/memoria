using FluentAssertions;

using Memoria.Users.Audit;
using Memoria.Users.UnitTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Memoria.Users.UnitTests.Audit;

public sealed class AuditLoggerTests
{
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task LogAsyncPersistsRowWithSerializedMetadata()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var sut = new AuditLogger(db, _clock, NullLogger<AuditLogger>.Instance);
        var actor = Guid.NewGuid();

        await sut.LogAsync(
            actor,
            action: "admin.users.list",
            subject: "users",
            metadata: new { page = 1, pageSize = 25 },
            CancellationToken.None);

        var row = await db.AuditLog.SingleAsync();
        row.ActorUserId.Should().Be(actor);
        row.Action.Should().Be("admin.users.list");
        row.Subject.Should().Be("users");
        row.OccurredAt.Should().Be(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        row.MetadataJson.Should().Be("{\"page\":1,\"pageSize\":25}");
    }

    [Fact]
    public async Task LogAsyncWithNullMetadataPersistsNullJson()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var sut = new AuditLogger(db, _clock, NullLogger<AuditLogger>.Instance);

        await sut.LogAsync(
            Guid.NewGuid(),
            action: "admin.overview.read",
            subject: "overview",
            metadata: null,
            CancellationToken.None);

        (await db.AuditLog.SingleAsync()).MetadataJson.Should().BeNull();
    }
}
