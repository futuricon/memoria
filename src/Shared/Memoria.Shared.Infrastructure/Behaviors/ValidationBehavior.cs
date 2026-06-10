using System.Reflection;

using FluentValidation;

using MediatR;

using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Logging;

namespace Memoria.Shared.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior, который запускает все зарегистрированные
/// <see cref="IValidator{T}"/> для команды/запроса и, при ошибках валидации,
/// возвращает <c>Result&lt;T&gt;.Failure(Error.Validation(...))</c> вместо вызова handler-а.
/// </summary>
/// <remarks>
/// Работает только когда <typeparamref name="TResponse"/> — это <c>Result&lt;T&gt;</c>.
/// Для других возвращаемых типов поведение прозрачное (валидация запускается,
/// но при провале выбрасывается <see cref="ValidationException"/>).
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(validators);
        ArgumentNullException.ThrowIfNull(logger);
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (!_validators.Any())
        {
            return await next().ConfigureAwait(false);
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))).ConfigureAwait(false);

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next().ConfigureAwait(false);
        }

        _logger.LogWarning(
            "Validation failed for {RequestType} with {ErrorCount} error(s): {Errors}",
            typeof(TRequest).Name,
            failures.Count,
            failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));

        var firstFailure = failures[0];
        var error = Error.Validation(
            code: $"validation.{firstFailure.PropertyName}",
            message: string.Join("; ", failures.Select(f => f.ErrorMessage)));

        if (TryBuildFailureResult(error, out var failureResponse))
        {
            return failureResponse;
        }

        throw new ValidationException(failures);
    }

    private static bool TryBuildFailureResult(Error error, out TResponse response)
    {
        var responseType = typeof(TResponse);
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var failureMethod = responseType.GetMethod(
                nameof(Result<object>.Failure),
                BindingFlags.Public | BindingFlags.Static);

            if (failureMethod is not null)
            {
                response = (TResponse)failureMethod.Invoke(null, new object[] { error })!;
                return true;
            }
        }

        response = default!;
        return false;
    }
}
