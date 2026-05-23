namespace Memoria.Shared.Kernel.Results;

/// <summary>
/// Результат операции: либо успешное значение, либо ошибка.
/// </summary>
/// <remarks>
/// Технические исключения (DbUpdateException, недоступность Telegram API) НЕ оборачиваются
/// в Result — они ловятся middleware. Result — для ожидаемых доменных и валидационных
/// ошибок, которые нужно вернуть пользователю осмысленно.
/// </remarks>
public readonly record struct Result<T>
{
    public T? Value { get; init; }
    public Error? Error { get; init; }

    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    public static Result<T> Success(T value) => new() { Value = value };
    public static Result<T> Failure(Error error) => new() { Error = error };

    /// <summary>Неявная конвертация значения в успешный результат.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Неявная конвертация ошибки в результат-сбой.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);
}