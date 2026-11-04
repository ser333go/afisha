using FluentValidation;

namespace Afisha.Api.Validation;

/// <summary>
/// Endpoint-фильтр: валидирует тело запроса и возвращает 400 в формате ProblemDetails с объектом errors.
/// </summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(model, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            return Results.ValidationProblem(result.ToErrorDictionary());
        }

        return await next(context);
    }
}
