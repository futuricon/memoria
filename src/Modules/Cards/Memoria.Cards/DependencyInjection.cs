using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Memoria.Cards.Persistence;
using Memoria.Cards.Services;

namespace Memoria.Cards;

public static class DependencyInjection
{
    public static IServiceCollection AddCardsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Postgres")
                               ?? throw new InvalidOperationException(
                                   "Connection string 'Postgres' is not configured.");

        services.AddDbContext<CardsDbContext>(options =>
            options
                .UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable(
                        tableName: "__ef_migrations_history",
                        schema: CardsDbContext.SchemaName))
                .UseSnakeCaseNamingConvention());

        services.AddSingleton<TagNormalizer>();
        services.AddScoped<TagRepository>();

        return services;
    }

    public static async Task MigrateCardsModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var db = services.GetRequiredService<CardsDbContext>();
        await db.Database.MigrateAsync();
    }
}
