using SpeakMate.Application.Common;

namespace SpeakMate.Api.Common;

/// <summary>Translates application results into HTTP responses (RFC 9457 problem details).</summary>
internal static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            return result.Error!.ToProblem();
        }

        return onSuccess is null ? TypedResults.Ok(result.Value) : onSuccess(result.Value);
    }

    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.FieldErrors is not null)
        {
            extensions["errors"] = error.FieldErrors;
        }

        return TypedResults.Problem(title: error.Message, statusCode: status, extensions: extensions);
    }
}
