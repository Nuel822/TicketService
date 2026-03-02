using TicketService.Domain.Entities;

namespace TicketService.Application.Common.Interfaces;

public interface IIdempotencyStore
{

    Task<IdempotencyKey?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SaveAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default);
}


