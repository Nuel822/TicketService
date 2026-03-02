using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace TicketService.API.Filters;

/// <summary>
/// Endpoint filter that runs FluentValidation on the first argument of type T
/// before the endpoint handler executes.
///
/// Usage on a Minimal API endpoint:
///   .AddEndpointFilter<ValidationFilter<CreateEventRequest>>()
///
/// If validation fails, returns 422 Unprocessable Entity with a Problem Details body
/// listing all validation errors keyed by field name.
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    private readonly IValidator<T> _validator;

    public ValidationFilter(IValidator<T> validator)
    {
        _validator = validator;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Find the first argument of type T in the endpoint's parameter list
        var argument = context.Arguments
            .OfType<T>()
            .FirstOrDefault();

        if (argument is null)
            return await next(context);

        var result = await _validator.ValidateAsync(argument, context.HttpContext.RequestAborted);

        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            var problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Validation Failed",
                Detail = "One or more validation errors occurred.",
                Instance = context.HttpContext.Request.Path
            };

            return Results.Json(problem, statusCode: StatusCodes.Status422UnprocessableEntity,
                contentType: "application/problem+json");
        }

        return await next(context);
    }
}


