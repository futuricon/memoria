namespace Memoria.Shared.Kernel.Results;

/// <summary>
/// Категория ошибки. Используется presentation-слоем для маппинга
/// на HTTP-статусы или текст для пользователя.
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Unexpected,
}