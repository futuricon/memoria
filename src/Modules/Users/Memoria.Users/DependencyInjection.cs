using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Memoria.Users.Persistence;

namespace Memoria.Users;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Postgres")
                               ?? throw new InvalidOperationException(
                                   "Connection string 'Postgres' is not configured.");

        services.AddDbContext<UsersDbContext>(options =>
            options
                .UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable(
                        tableName: "__ef_migrations_history",
                        schema: UsersDbContext.SchemaName))
                .UseSnakeCaseNamingConvention());

        return services;
    }
    
    public static async Task MigrateUsersModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var db = services.GetRequiredService<UsersDbContext>();
        await db.Database.MigrateAsync();
    }

}