using Microsoft.EntityFrameworkCore;
using TicketService.Application.Common.Interfaces;
using TicketService.Domain.Entities;
using TicketService.Infrastructure.Persistence.TicketingDb;

namespace TicketService.Infrastructure.Repositories;

public class IdempotencyStore : IIdempotencyStore
{
    private readonly TicketingDbContext _context;

    public IdempotencyStore(TicketingDbContext context)
    {
        _context = context;
    }

    public async Task<IdempotencyKey?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var record = await _context.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key == key, cancellationToken);

        // Return null if expired so the caller treats it as a fresh request
        if (record == null || record.IsExpired())
            return null;

        return record;
    }

    public async Task SaveAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default)
    {
        _context.IdempotencyKeys.Add(idempotencyKey);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Two concurrent requests with the same key raced to insert.
            // The unique index on IdempotencyKeys.Key means only one wins.
            // The loser silently discards — the winner's stored response will
            // be returned to both callers on the next GetAsync call.
            _context.Entry(idempotencyKey).State = EntityState.Detached;
        }
    }

    // PostgreSQL unique constraint violation = SQLSTATE 23505
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
        || ex.InnerException?.Message.Contains("unique constraint") == true
        || ex.InnerException?.Message.Contains("duplicate key") == true;
}


