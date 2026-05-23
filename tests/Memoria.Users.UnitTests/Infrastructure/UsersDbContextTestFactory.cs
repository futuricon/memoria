using Microsoft.EntityFrameworkCore;

using Memoria.Users.Persistence;

namespace Memoria.Users.UnitTests.Infrastructure;

/// <summary>
/// Создаёт изолированный <see cref="UsersDbContext"/> поверх EF InMemory.
/// Каждый вызов — новая БД (по уникальному имени) — тесты не пересекаются.
/// </summary>
internal static class UsersDbContextTestFactory
{
    public static UsersDbContext Create()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .EnableSensitiveDataLogging()
            .Options;

        return new UsersDbContext(options);
    }
}
