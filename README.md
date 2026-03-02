# Ticket Service API

A REST API for a simplified event ticketing system built with **.NET 10**, **PostgreSQL**, and **Clean Architecture**.

---

## What It Does

- **Event Management** — Create, read, update, and delete events with pricing tiers
- **Ticket Purchasing** — Buy tickets with real-time inventory tracking and oversell prevention
- **Availability** — Check remaining ticket counts per event and pricing tier
- **Sales Reports** — Paginated sales summaries per event

---

## Prerequisites

- [Docker](https://www.docker.com/get-started) and Docker Compose
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) _(local dev only)_

---

## Running with Docker (Recommended)

```bash
docker compose up --build
```

The API will be available at `http://localhost:8080`.  

To stop and remove all containers:

```bash
docker compose down

# Remove the database volume too (fresh start):
docker compose down -v
```

---

## Running Locally

### 1. Start PostgreSQL

```bash
docker run -d \
  --name ticketing-postgres \
  -e POSTGRES_DB=ticketing \
  -e POSTGRES_USER=ticketing_user \
  -e POSTGRES_PASSWORD=ticketing_pass \
  -p 5432:5432 \
  -v ./init.sql:/docker-entrypoint-initdb.d/init.sql:ro \
  postgres:16-alpine
```

### 2. Install the EF Core CLI

```bash
dotnet tool install --global dotnet-ef --version 10.0.0
```

### 3. Apply migrations

```bash
dotnet ef database update \
  --project src/TicketService.Infrastructure \
  --startup-project src/TicketService.API \
  --context TicketingDbContext

dotnet ef database update \
  --project src/TicketService.Infrastructure \
  --startup-project src/TicketService.API \
  --context ReportingDbContext
```

### 4. Run the API

```bash
cd src/TicketService.API
dotnet run
```

---

## Running Tests

```bash
cd tests/TicketService.UnitTests
dotnet test
```

Unit tests cover domain logic, validators, commands, queries, idempotency, rate limiting, and concurrent oversell prevention.

---

## API Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/events` | List all events |
| `GET` | `/api/events/{id}` | Get a single event |
| `POST` | `/api/events` | Create an event |
| `PUT` | `/api/events/{id}` | Update an event |
| `DELETE` | `/api/events/{id}` | Delete an event |
| `GET` | `/api/events/{eventId}/tickets/availability` | Check ticket availability |
| `POST` | `/api/events/{eventId}/tickets` | Purchase tickets |
| `GET` | `/api/reports/events/{eventId}/sales` | Sales report for one event |
| `GET` | `/api/reports/events/sales?page=1&pageSize=20` | Paginated sales report for all events |

---

## Example Requests

### Create an Event

```http
POST /api/events
Content-Type: application/json

{
  "name": "Summer Music Festival",
  "description": "An outdoor music festival.",
  "venue": "Hyde Park, London",
  "date": "2026-07-15",
  "time": "18:00:00",
  "totalCapacity": 500,
  "pricingTiers": [
    { "name": "General Admission", "price": 50.00, "quantity": 400 },
    { "name": "VIP", "price": 150.00, "quantity": 100 }
  ]
}
```

### Purchase Tickets

```http
POST /api/events/{eventId}/tickets
Content-Type: application/json
Idempotency-Key: <unique-uuid>

{
  "pricingTierId": "<tier-uuid>",
  "purchaserName": "Alice Smith",
  "purchaserEmail": "alice@example.com",
  "quantity": 2
}
```

The `Idempotency-Key` header is optional but recommended. Repeating the same request with the same key within 24 hours returns the original response without creating a duplicate ticket.

---

## Adding a New Migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/TicketService.Infrastructure \
  --startup-project src/TicketService.API \
  --context TicketingDbContext

dotnet ef migrations add <MigrationName> \
  --project src/TicketService.Infrastructure \
  --startup-project src/TicketService.API \
  --context ReportingDbContext
```

---

## Architecture & Design Decisions

### Clean Architecture (4 layers)

```
Domain  ←  Application  ←  Infrastructure  ←  API
```

- **Domain** — Pure C# entities and domain exceptions. No framework dependencies.
- **Application** — Commands, queries, validators, and repository interfaces. Depends only on Domain.
- **Infrastructure** — EF Core DbContexts, repository implementations, background services. Depends on Application.
- **API** — ASP.NET Core controllers, middleware, DI wiring. Depends on Application and Infrastructure.

### Dual-Database CQRS

The system uses two separate PostgreSQL databases:

| Database | Purpose |
|---|---|
| `ticketing` | Transactional write store — events, tickets, pricing tiers, outbox, idempotency keys |
| `ticketing_reporting` | Denormalised read store — pre-aggregated sales summaries |

**Why?** Read and write workloads have different shapes. The write path needs strong consistency and row-level locking. The read/reporting path needs fast aggregated queries without joining across many rows. Separating them allows each to be optimised independently.

**Trade-off:** Reporting data is eventually consistent (typically < 5 seconds behind). The API communicates this to callers via a `note` field in report responses.

### Oversell Prevention

Two complementary layers prevent selling more tickets than available:

1. **Pessimistic lock (`SELECT ... FOR UPDATE`)** — The primary defence. When a purchase request arrives, the `pricing_tiers` row is locked for the duration of the transaction. Concurrent requests for the same tier queue behind the lock, ensuring only one decrements availability at a time.

2. **Optimistic concurrency token (`xmin`)** — PostgreSQL's built-in `xmin` system column is used as a row version. If two transactions somehow bypass the pessimistic lock (e.g. different application instances with a race), EF Core's concurrency check will cause one to throw a `DbUpdateConcurrencyException`.

### Outbox Pattern

Ticket purchase events are written to an `outbox_messages` table **in the same transaction** as the ticket insert. A background service (`OutboxProcessor`) polls this table every 5 seconds and applies unprocessed events to the Reporting DB.

**Why?** This guarantees that the reporting database is always updated — even if the application crashes immediately after a purchase. Without the outbox, a crash between the ticket insert and the reporting update would leave the two databases permanently inconsistent.

**Trade-off:** Reporting data is eventually consistent, not immediately consistent.

### Idempotency

The `POST /api/events/{eventId}/tickets` endpoint accepts an optional `Idempotency-Key` header. If the same key is presented within 24 hours, the original response is returned without creating a duplicate ticket. This protects against network retries causing double-purchases.

The idempotency key is stored in the same `ticketing` database with a unique constraint. A race condition where two concurrent requests present the same key is handled by catching the unique constraint violation and returning the stored response to both callers.

### Rate Limiting

Three policies protect the API from abuse:

| Policy | Algorithm | Limit | Window | Rationale |
|---|---|---|---|---|
| `reads` | Fixed window | 200 req | 10 s | Read-heavy traffic; generous limit |
| `writes` | Fixed window | 60 req | 10 s | Mutations are more expensive |
| `purchases` | Sliding window | 5 req | 60 s | Anti-scalping; tightest limit |

The sliding window for purchases prevents burst attacks at window boundaries (e.g. 5 requests at 0:59 + 5 at 1:00).