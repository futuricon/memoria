namespace Memoria.Shared.Kernel.Results;

/// <summary>
/// Описание ошибки доменной или прикладной операции.
/// </summary>
/// <param name="Code">Стабильный машинный код вида "cards.title_too_long". Не локализуется.</param>
/// <param name="Message">Человекочитаемое сообщение. Может локализоваться presentation-слоем.</param>
/// <param name="Type">Категория ошибки для маппинга на HTTP-статус.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message)   => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message)     => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message)     => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message)    => new(code, message, ErrorType.Forbidden);
    public static Error Unexpected(string code, string message)   => new(code, message, ErrorType.Unexpected);
}