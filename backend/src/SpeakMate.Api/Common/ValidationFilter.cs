using FluentValidation;
using SpeakMate.Application.Common;

namespace SpeakMate.Api.Common;

/// <summary>Runs the FluentValidation validator for <typeparamref name="TRequest"/> before the endpoint executes.</summary>
internal sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return Error.Validation("request.body_required", "A request body is required.").ToProblem();
        }

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (validation.IsValid)
        {
            return await next(context).ConfigureAwait(false);
        }

        var fields = validation.Errors
            .GroupBy(e => JsonNamingPolicyCamel(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return Error.Validation("request.invalid", "Some fields need attention.", fields).ToProblem();
    }

    private static string JsonNamingPolicyCamel(string name) =>
        System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(name);
}

internal static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder Validate<TRequest>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationFilter<TRequest>>().ProducesValidationProblem();
}
