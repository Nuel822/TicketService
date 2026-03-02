using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TicketService.Infrastructure.BackgroundServices;
using TicketService.Infrastructure.Persistence.ReportingDb;
using TicketService.Infrastructure.Persistence.TicketingDb;

namespace TicketService.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that:
///   1. Replaces both PostgreSQL DbContexts with EF Core InMemory providers
///      so integration tests run without a real database.
///   2. Removes the OutboxProcessor background service to prevent it from
///      interfering with test assertions (the OutboxProcessor polls every 5 s
///      and would mutate the ReportingDb asynchronously during tests).
///   3. Ensures both InMemory databases are created before each test run.
/// </summary>
public class TicketingWebApplicationFactory : WebApplicationFactory<Program>
{
    // Use a unique DB name per factory instance so parallel test classes
    // don't share state.
    private readonly string _dbName = $"TicketingTestDb_{Guid.NewGuid()}";
    private readonly string _reportingDbName = $"ReportingTestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ── Remove the real PostgreSQL TicketingDbContext ─────────────────
            services.RemoveAll<DbContextOptions<TicketingDbContext>>();
            services.RemoveAll<TicketingDbContext>();

            // ── Remove the real PostgreSQL ReportingDbContext ─────────────────
            services.RemoveAll<DbContextOptions<ReportingDbContext>>();
            services.RemoveAll<ReportingDbContext>();

            // ── Remove the OutboxProcessor hosted service ─────────────────────
            // We don't want the background processor running during tests because:
            //   a) It would try to connect to a real PostgreSQL instance.
            //   b) It would asynchronously mutate the ReportingDb, making
            //      deterministic assertions on reporting data impossible.
            services.RemoveAll<IHostedService>();

            // ── Register InMemory TicketingDbContext ──────────────────────────
            services.AddDbContext<TicketingDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // ── Register InMemory ReportingDbContext ──────────────────────────
            services.AddDbContext<ReportingDbContext>(options =>
                options.UseInMemoryDatabase(_reportingDbName));
        });

        // Ensure both InMemory databases are created (schema is auto-created
        // by EF Core InMemory provider on first EnsureCreated call).
        builder.Configure(app =>
        {
            using var scope = app.ApplicationServices.CreateScope();
            var ticketingDb = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
            ticketingDb.Database.EnsureCreated();

            var reportingDb = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
            reportingDb.Database.EnsureCreated();
        });
    }
}


