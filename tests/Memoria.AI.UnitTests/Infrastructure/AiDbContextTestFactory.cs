using Microsoft.EntityFrameworkCore;

using Memoria.AI.Persistence;

namespace Memoria.AI.UnitTests.Infrastructure;

internal static class AiDbContextTestFactory
{
    public static AiDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .EnableSensitiveDataLogging()
            .Options;

        return new AiDbContext(options);
    }
}
