using Microsoft.AspNetCore.Diagnostics;
using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Common;

namespace SpeakMate.Api.Common;

/// <summary>
/// Converts known exception types into safe problem responses. Anything else becomes a generic
/// 500 with no internal details; the full exception is logged server-side only.
/// </summary>
internal sealed partial class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            DomainException domain => (StatusCodes.Status400BadRequest, domain.Message, domain.Code),
            ConcurrencyConflictException conflict => (StatusCodes.Status409Conflict, conflict.Message, "request.conflict"),
            BadHttpRequestException bad => (bad.StatusCode, "The request could not be read.", "request.malformed"),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested =>
                (499, "Request cancelled.", "request.cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong on our side. Please try again.",
                "server.error"),
        };

        if (status >= 500)
        {
            LogUnhandled(exception, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                Extensions = { ["code"] = code },
            },
        }).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private partial void LogUnhandled(Exception exception, string method, string path);
}
