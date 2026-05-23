using Microsoft.EntityFrameworkCore;

using Memoria.Cards.Persistence;

namespace Memoria.Cards.UnitTests.Infrastructure;

internal static class CardsDbContextTestFactory
{
    public static CardsDbContext Create()
    {
        var options = new DbContextOptionsBuilder<CardsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .EnableSensitiveDataLogging()
            .Options;

        return new CardsDbContext(options);
    }
}
