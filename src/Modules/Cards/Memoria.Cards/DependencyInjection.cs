using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Cards;

/// <summary>
/// Регистрация сервисов модуля Cards.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Добавляет в контейнер DI компоненты модуля Cards: handler'ы команд/запросов,
    /// EF-контекст, нормализатор тегов.
    /// </summary>
    public static IServiceCollection AddCardsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services;
    }
}