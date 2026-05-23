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

        return services;
    }
}