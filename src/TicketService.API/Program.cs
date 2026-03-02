using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TicketService.API.Endpoints;
using TicketService.API.Middleware;
using TicketService.Application;
using TicketService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Application & Infrastructure services ────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Ticket Service API",
        Version = "v1",
        Description = "REST API for a simplified event ticketing system."
    });
});

// ── Rate Limiting ─────────────────────────────────────────────────────────────
// Three policies, all partitioned by client IP address:
//
//   "reads"     — Fixed window:   200 req / 10 s
//                 High limit for read-heavy traffic (event listings, availability checks).
//
//   "writes"    — Fixed window:    60 req / 10 s
//                 Moderate limit for event management (create/update/delete).
//
//   "purchases" — Sliding window:   5 req / 60 s
//                 Tight limit to deter ticket-scalping bots and bulk-purchase abuse.
//                 Sliding window is used (vs fixed) so bursts at window boundaries
//                 are also throttled — a client cannot fire 5 requests at 0:59
//                 and another 5 at 1:00 to get 10 in quick succession.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("reads", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromSeconds(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("writes", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromSeconds(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("purchases", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,           // max 5 purchase attempts per minute per IP
                Window = TimeSpan.FromSeconds(60),
                SegmentsPerWindow = 6,     // 6 × 10-second segments for smooth sliding
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
// GlobalExceptionMiddleware must be registered first so it wraps all subsequent
// middleware and endpoint handlers, catching any unhandled exceptions.
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger UI is available in all environments for ease of use
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Ticket Service API v1");
    options.RoutePrefix = string.Empty; // Serve Swagger UI at http://localhost:8080/
});

app.UseRateLimiter();

// ── Endpoint registration ─────────────────────────────────────────────────────
app.MapGroup("/api/events")
    .WithTags("Events")
    .MapEventEndpoints();

app.MapGroup("/api/events")
    .WithTags("Tickets")
    .MapTicketEndpoints();

app.MapGroup("/api/reports")
    .WithTags("Reports")
    .MapReportEndpoints();

app.Run();

// Expose Program to integration tests via WebApplicationFactory<Program>
public partial class Program { }


