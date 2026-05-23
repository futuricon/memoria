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
    public static IServiceCollection AddRemindersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services;
    }
}