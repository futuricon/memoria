using Memoria.Reminders.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Reminders;

/// <summary>
/// Регистрация сервисов модуля Reminders.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Добавляет в контейнер DI компоненты модуля Reminders: планировщик,
    /// Hangfire-задачи, EF-контекст.
    /// </summary>
    public static IServiceCollection AddRemindersModule(this IServiceCollection services, IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("…");

        services.AddDbContext<RemindersDbContext>(o =>
            o.UseNpgsql(cs, n => n.MigrationsHistoryTable("__ef_migrations_history", RemindersDbContext.SchemaName))
             .UseSnakeCaseNamingConvention());

        return services;
    }
}