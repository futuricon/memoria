using Memoria.Reviews.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Reviews;

/// <summary>
/// Регистрация сервисов модуля Reviews.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Добавляет в контейнер DI компоненты модуля Reviews: handler'ы команд,
    /// EF-контекст.
    /// </summary>
    public static IServiceCollection AddReviewsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddDbContext<ReviewsDbContext>(o =>
            o.UseNpgsql(connectionString, n => n.MigrationsHistoryTable(
                tableName: "__ef_migrations_history",
                schema: ReviewsDbContext.SchemaName))
             .UseSnakeCaseNamingConvention());

        return services;
    }

    /// <summary>
    /// Применяет ожидающие миграции <c>ReviewsDbContext</c>. Вызывается из
    /// <c>Program.cs</c> внутри scope сразу после построения хоста.
    /// </summary>
    public static async Task MigrateReviewsModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var db = services.GetRequiredService<ReviewsDbContext>();
        await db.Database.MigrateAsync();
    }
}
