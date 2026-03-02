using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketService.Application.Common.Exceptions;
using TicketService.Domain.Exceptions;

namespace TicketService.API.Middleware;

/// <summary>
/// Catches all unhandled exceptions and maps them to RFC 7807 Problem Details responses.
///
/// Mapping table:
///   NotFoundException              → 404 Not Found
///   DomainException (OversellException, InvalidTicketStateException) → 409 Conflict
///   DbUpdateConcurrencyException   → 409 Conflict  (optimistic concurrency on PricingTier.RowVersion)
///   ValidationException            → 422 Unprocessable Entity  (FluentValidation)
///   OperationCanceledException     → 499 Client Closed Request (no body)
///   Everything else                → 500 Internal Server Error
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — no response body needed
            context.Response.StatusCode = 499;
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found: {Message}", ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message);
        }
        catch (DomainException ex)
        {
            // Covers OversellException and InvalidTicketStateException
            _logger.LogWarning(ex, "Domain rule violation: {Message}", ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Optimistic concurrency conflict on PricingTier.RowVersion (xmin)
            _logger.LogWarning(ex, "Concurrency conflict detected.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Conflict",
                "The resource was modified by another request. Please retry.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please try again later.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}


