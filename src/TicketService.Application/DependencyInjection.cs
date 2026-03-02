using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TicketService.Application.Events.Commands;
using TicketService.Application.Events.Queries;
using TicketService.Application.Events.Validators;
using TicketService.Application.Tickets.Commands;
using TicketService.Application.Tickets.Queries;
using TicketService.Application.Tickets.Validators;

namespace TicketService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register validators
        services.AddScoped<IValidator<CreateEventRequest>, CreateEventValidator>();
        services.AddScoped<IValidator<UpdateEventRequest>, UpdateEventValidator>();
        services.AddScoped<IValidator<PurchaseTicketRequest>, PurchaseTicketValidator>();

        // Register event commands
        services.AddScoped<CreateEventCommand>();
        services.AddScoped<UpdateEventCommand>();
        services.AddScoped<DeleteEventCommand>();

        // Register event queries
        services.AddScoped<GetEventByIdQuery>();
        services.AddScoped<GetAllEventsQuery>();

        // Register ticket commands
        services.AddScoped<PurchaseTicketCommand>();

        // Register ticket queries
        services.AddScoped<GetTicketAvailabilityQuery>();
        services.AddScoped<GetSalesReportQuery>();

        return services;
    }
}


