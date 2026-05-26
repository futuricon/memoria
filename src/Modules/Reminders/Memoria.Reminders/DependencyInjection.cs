using Hangfire;
using Hangfire.PostgreSql;

using Memoria.Reminders.Options;
using Memoria.Reminders.Persistence;
using Memoria.Reminders.Services;

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
    /// Добавляет в контейнер DI компоненты модуля Reminders: EF-контекст,
    /// настройки, планировщик, Hangfire-storage (сервер регистрируется в Host).
    /// </summary>
    public static IServiceCollection AddRemindersModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddDbContext<RemindersDbContext>(o =>
            o.UseNpgsql(connectionString, n => n.MigrationsHistoryTable(
                tableName: "__ef_migrations_history",
                schema: RemindersDbContext.SchemaName))
             .UseSnakeCaseNamingConvention());

        services.AddOptions<RemindersOptions>()
            .Bind(configuration.GetSection(RemindersOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ReminderScheduler>();

        services.AddHangfire(cfg =>
        {
            cfg
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString));
        });

        return services;
    }
}
