using Microsoft.EntityFrameworkCore;
using TicketService.Domain.Entities;
using TicketService.Infrastructure.Persistence.ReportingDb.Configurations;

namespace TicketService.Infrastructure.Persistence.ReportingDb;

public class ReportingDbContext : DbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options) { }

    public DbSet<EventSalesSummary> EventSalesSummaries => Set<EventSalesSummary>();
    public DbSet<TierSalesSummary> TierSalesSummaries => Set<TierSalesSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new EventSalesSummaryConfiguration());
        modelBuilder.ApplyConfiguration(new TierSalesSummaryConfiguration());
    }
}


