using Memoria.Reminders.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.UnitTests.Infrastructure;

/// <summary>
/// Creates an isolated <see cref="RemindersDbContext"/> backed by EF Core
/// InMemory. Each call returns a fresh database (unique Guid name) — tests
/// don't share state.
/// </summary>
internal static class RemindersDbContextTestFactory
{
    public static RemindersDbContext Create()
    {
        var options = new DbContextOptionsBuilder<RemindersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .EnableSensitiveDataLogging()
            .Options;

        return new RemindersDbContext(options);
    }
}
