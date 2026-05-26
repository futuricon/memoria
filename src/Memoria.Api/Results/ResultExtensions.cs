using Memoria.Shared.Kernel.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Memoria.Api.Results;

/// <summary>
/// Маппит <see cref="Result{T}"/> в <see cref="IResult"/>. Живёт в Api, а не в
/// Shared.Kernel, чтобы Kernel оставался framework-agnostic.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is not null
                ? onSuccess(result.Value!)
                : Microsoft.AspNetCore.Http.Results.Ok(result.Value);
        }

        var err = result.Error!;
        var problem = new ProblemDetails
        {
            Title = err.Code,
            Detail = err.Message,
            Status = MapStatusCode(err.Type),
        };
        return Microsoft.AspNetCore.Http.Results.Problem(problem);
    }

    public static IResult ToHttpResultNoContent<T>(this Result<T> result)
    {
        return result.IsSuccess
            ? Microsoft.AspNetCore.Http.Results.NoContent()
            : result.ToHttpResult();
    }

    private static int MapStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError,
    };
}
