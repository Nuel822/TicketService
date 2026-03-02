using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketService.Application.Common.Interfaces;
using TicketService.Infrastructure.BackgroundServices;
using TicketService.Infrastructure.Persistence.ReportingDb;
using TicketService.Infrastructure.Persistence.TicketingDb;
using TicketService.Infrastructure.Repositories;

namespace TicketService.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure-layer services:
    ///   - TicketingDbContext  (primary transactional DB)
    ///   - ReportingDbContext  (read-optimised reporting DB)
    ///   - Repository implementations
    ///   - IdempotencyStore
    ///   - OutboxProcessor background service
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Primary (Ticketing) DB ────────────────────────────────────────────
        services.AddDbContext<TicketingDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("TicketingDb"),
                npgsql => npgsql.MigrationsAssembly(typeof(TicketingDbContext).Assembly.FullName)));

        // ── Reporting DB ──────────────────────────────────────────────────────
        services.AddDbContext<ReportingDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("ReportingDb"),
                npgsql => npgsql.MigrationsAssembly(typeof(ReportingDbContext).Assembly.FullName)));

        // ── Repositories ──────────────────────────────────────────────────────
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();

        // ── Idempotency Store ─────────────────────────────────────────────────
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();

        // ── Background Services ───────────────────────────────────────────────
        // OutboxProcessor is a singleton IHostedService.
        // It uses IServiceScopeFactory internally to resolve scoped DbContexts
        // per processing cycle, avoiding captive dependency issues.
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}


