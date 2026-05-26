using Memoria.Reviews.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.UnitTests.Infrastructure;

/// <summary>
/// Creates an isolated <see cref="ReviewsDbContext"/> backed by EF Core
/// InMemory. Each call returns a fresh database (unique Guid name) — tests
/// don't share state.
/// </summary>
internal static class ReviewsDbContextTestFactory
{
    public static ReviewsDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ReviewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .EnableSensitiveDataLogging()
            .Options;

        return new ReviewsDbContext(options);
    }
}
