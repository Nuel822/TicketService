# Ticket Service API

A production-grade REST API for a simplified event ticketing system, built with **.NET 8 ASP.NET Core Minimal APIs**, **PostgreSQL**, and **Clean Architecture**.

---

## Table of Contents

1. [What the Project Does](#what-the-project-does)
2. [Architecture Overview](#architecture-overview)
3. [Key Design Decisions](#key-design-decisions)
4. [API Endpoints](#api-endpoints)
5. [Prerequisites](#prerequisites)
6. [Running with Docker (Recommended)](#running-with-docker-recommended)
7. [Running Locally (Without Docker)](#running-locally-without-docker)
8. [Running Tests](#running-tests)
9. [Project Structure](#project-structure)
10. [Future Improvements](#future-improvements)

---

## What the Project Does

The Ticket Service API provides a complete backend for managing events and selling tickets. It supports:

- **Event Management** — Create, retrieve, update, and delete events with pricing tiers
- **Ticket Purchasing** — Buy tickets for an event with real-time inventory tracking
- **Availability Checking** — View remaining ticket counts per event and per pricing tier
- **Sales Reporting** — Generate paginated sales summaries per event (eventually consistent)
- **Oversell Prevention** — Concurrent purchases are serialised at the database level to guarantee inventory accuracy
- **Idempotent Purchases** — Clients can safely retry purchase requests using an `Idempotency-Key` header
- **Rate Limiting** — Three tiered policies protect against abuse and scalping bots

---

## Architecture Overview

The solution follows **Clean Architecture** with four layers, each depending only inward:

```
┌─────────────────────────────────────────────────────────┐
│  API Layer          (TicketService.API)                  │
│  Minimal API endpoints, middleware, filters, rate limits │
├─────────────────────────────────────────────────────────┤
│  Application Layer  (TicketService.Application)          │
│  Commands, queries, validators, interfaces               │
├─────────────────────────────────────────────────────────┤
│  Infrastructure Layer (TicketService.Infrastructure)     │
│  EF Core DbContexts, repositories, OutboxProcessor       │
├─────────────────────────────────────────────────────────┤
│  Domain Layer       (TicketService.Domain)               │
│  Entities, enums, domain exceptions                      │
└─────────────────────────────────────────────────────────┘
```

### Dual-Database Design

| Database | Purpose |
|---|---|
| `ticketing` | Primary transactional database — all writes go here |
| `ticketing_reporting` | Read-optimised reporting database — eventually consistent |

The two databases are hosted on the **same PostgreSQL instance** (two separate logical databases). The `OutboxProcessor` background service replicates ticket purchase events from the primary database to the reporting database asynchronously via the **Transactional Outbox Pattern**.

---

## Key Design Decisions

### 1. Clean Architecture + CQRS (without MediatR)

Commands and queries are plain C# classes injected directly into Minimal API endpoint handlers via the DI container. MediatR was deliberately omitted to keep the dependency graph simple and avoid the overhead of a mediator for a service of this scope.

### 2. Minimal APIs with `RouteGroupBuilder`

ASP.NET Core 8 Minimal APIs were chosen over MVC controllers for their lower ceremony and better performance. Endpoints are organised into extension methods on `RouteGroupBuilder` (`EventEndpoints`, `TicketEndpoints`, `ReportEndpoints`) for maintainability.

### 3. Oversell Prevention — Pessimistic Locking

`TicketRepository.PurchaseAsync` uses a PostgreSQL `SELECT ... FOR UPDATE` pessimistic lock on the `PricingTier` row. This serialises concurrent purchase requests for the same tier at the database level, making overselling impossible regardless of how many API instances are running.

The domain entities (`PricingTier.DecrementAvailability`, `Event.DecrementAvailability`) provide a secondary guard that throws `OversellException` if the lock somehow fails — defence in depth.

### 4. Transactional Outbox Pattern

When a ticket is purchased, an `OutboxMessage` record is inserted in the **same database transaction** as the `Ticket` record. This guarantees that the reporting database is always eventually updated — even if the process crashes immediately after the purchase.

The `OutboxProcessor` background service polls for unprocessed outbox messages every 5 seconds (batch of 50), applies them to the reporting database, and marks them processed. Messages that fail are retried up to 5 times before being dead-lettered.

### 5. Idempotency

Clients can include an `Idempotency-Key` header on purchase requests. The key is stored as the `CorrelationId` on the `OutboxMessage`, providing end-to-end traceability from HTTP request to reporting event.

The `IdempotencyStore` uses a unique database constraint on `IdempotencyKeys.Key` to handle concurrent duplicate requests atomically — the first writer wins, the loser silently discards (catching SQLSTATE 23505).

### 6. Rate Limiting

Three policies, all partitioned by client IP address:

| Policy | Algorithm | Limit | Window | Rationale |
|---|---|---|---|---|
| `reads` | Fixed window | 200 req | 10 s | High limit for read-heavy traffic |
| `writes` | Fixed window | 60 req | 10 s | Moderate limit for event management |
| `purchases` | Sliding window | 5 req | 60 s | Tight anti-scalping limit |

The `purchases` policy uses a **sliding window** (vs fixed) so burst attacks at window boundaries are also throttled.

### 7. Validation

FluentValidation validators live in the Application layer and are applied via a `ValidationFilter<T>` endpoint filter. Invalid requests return `422 Unprocessable Entity` with RFC 7807 `ValidationProblemDetails`.

A cross-field rule on `CreateEventValidator` ensures the sum of pricing tier quantities equals `TotalCapacity`.

### 8. Error Handling

`GlobalExceptionMiddleware` maps exceptions to HTTP status codes using RFC 7807 Problem Details:

| Exception | HTTP Status |
|---|---|
| `NotFoundException` | 404 Not Found |
| `DomainException` (incl. `OversellException`) | 409 Conflict |
| `DbUpdateConcurrencyException` | 409 Conflict |
| Unhandled exceptions | 500 Internal Server Error |

### 9. Pagination

`GET /api/reports/events/sales` returns a `PagedResult<T>` envelope with `page` (1-based, default 1) and `pageSize` (1–100, default 20) query parameters.

### 10. Docker — Single PostgreSQL Instance

Both the `ticketing` and `ticketing_reporting` databases are hosted on a single PostgreSQL container. The `docker/postgres/init.sql` script creates the second database on first startup. This reduces infrastructure cost for development and small deployments.

---

## API Endpoints

### Events

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/events` | List all events |
| `GET` | `/api/events/{id}` | Get a single event |
| `POST` | `/api/events` | Create a new event |
| `PUT` | `/api/events/{id}` | Update an event |
| `DELETE` | `/api/events/{id}` | Delete an event |

### Tickets

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/events/{eventId}/tickets/availability` | Get ticket availability |
| `POST` | `/api/events/{eventId}/tickets` | Purchase tickets |

### Reports

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/reports/events/{eventId}/sales` | Sales report for one event |
| `GET` | `/api/reports/events/sales?page=1&pageSize=20` | Paginated sales report for all events |

### Swagger UI

When running in Development mode, Swagger UI is available at `http://localhost:8080`.

---

## Prerequisites

- [Docker](https://www.docker.com/get-started) and Docker Compose (for Docker setup)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for local setup)
- [PostgreSQL 16](https://www.postgresql.org/) (for local setup)

---

## Running with Docker (Recommended)

```bash
# 1. Clone the repository
git clone <repo-url>
cd Ticket_Service

# 2. Start all services (PostgreSQL + migrations + API)
docker compose up --build

# 3. The API is now available at:
#    http://localhost:8080
#    Swagger UI: http://localhost:8080 (served at root in Development)
```

**What `docker compose up` does:**

1. Starts a single PostgreSQL 16 container
2. Runs `docker/postgres/init.sql` to create both databases (`ticketing` and `ticketing_reporting`)
3. Runs the `db-migrate` service to apply EF Core migrations to both databases
4. Builds and starts the `ticket-service-api` container

**Stop all services:**

```bash
docker compose down

# To also remove the database volume (fresh start):
docker compose down -v
```

---

## Running Locally (Without Docker)

### 1. Start PostgreSQL

```bash
# Using Docker just for the database:
docker run -d \
  --name ticketing-postgres \
  -e POSTGRES_DB=ticketing \
  -e POSTGRES_USER=ticketing_user \
  -e POSTGRES_PASSWORD=ticketing_pass \
  -p 5432:5432 \
  -v ./init.sql:/docker-entrypoint-initdb.d/init.sql:ro \
  postgres:16-alpine
```

### 2. Install the EF Core CLI tool

```bash
dotnet tool install --global dotnet-ef --version 8.0.0
```

### 3. Apply migrations

```bash
# Ticketing (primary) database
dotnet ef database update \
  --project src/TicketService.Infrastructure \
  --startup-project src/TicketService.API \
  --context TicketingDbContext

# Reporting database
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

The API starts at `http://localhost:5000` (or `https://localhost:5001`). Swagger UI is available at the root URL in Development mode.

---

## Running Tests

```bash
# Run all unit tests
cd tests/TicketService.UnitTests
dotnet test

# Run with verbose output
dotnet test --verbosity normal
```

**Test coverage (130 tests, all passing):**

| Area | Tests |
|---|---|
| Domain entities (Event, PricingTier, Ticket) | 28 |
| Domain — IdempotencyKey TTL / expiry | 10 |
| Domain — Concurrent oversell + OutboxMessage | 17 |
| Application validators (CreateEvent, PurchaseTicket) | 25 |
| Application commands (CreateEvent, PurchaseTicket) | 10 |
| Application commands — Idempotency key threading | 7 |
| Application queries (GetEventById, GetSalesReport) | 11 |
| Infrastructure — Rate limiting configuration | 13 |
| Placeholder | 1 |

---

## Project Structure

```
Ticket_Service/
├── src/
│   ├── TicketService.Domain/           # Entities, enums, domain exceptions
│   │   ├── Entities/
│   │   │   ├── Event.cs
│   │   │   ├── PricingTier.cs
│   │   │   ├── Ticket.cs
│   │   │   ├── OutboxMessage.cs
│   │   │   ├── IdempotencyKey.cs
│   │   │   ├── EventSalesSummary.cs
│   │   │   └── TierSalesSummary.cs
│   │   ├── Enums/
│   │   │   └── TicketStatus.cs
│   │   └── Exceptions/
│   │       ├── DomainException.cs
│   │       ├── OversellException.cs
│   │       └── InvalidTicketStateException.cs
│   │
│   ├── TicketService.Application/      # Commands, queries, validators, interfaces
│   │   ├── Common/
│   │   │   ├── Exceptions/             # NotFoundException
│   │   │   └── Interfaces/             # IEventRepository, ITicketRepository, etc.
│   │   ├── Events/
│   │   │   ├── Commands/               # CreateEvent, UpdateEvent, DeleteEvent
│   │   │   ├── Queries/                # GetEventById, GetAllEvents
│   │   │   └── Validators/             # CreateEventValidator
│   │   └── Tickets/
│   │       ├── Commands/               # PurchaseTicket
│   │       ├── Queries/                # GetTicketAvailability, GetSalesReport
│   │       └── Validators/             # PurchaseTicketValidator
│   │
│   ├── TicketService.Infrastructure/   # EF Core, repositories, background services
│   │   ├── BackgroundServices/
│   │   │   └── OutboxProcessor.cs      # Polls outbox, replicates to reporting DB
│   │   ├── Persistence/
│   │   │   ├── TicketingDb/            # Primary DB context + EF configurations
│   │   │   └── ReportingDb/            # Reporting DB context + EF configurations
│   │   └── Repositories/
│   │       ├── EventRepository.cs
│   │       ├── TicketRepository.cs     # SELECT FOR UPDATE pessimistic lock
│   │       ├── IdempotencyStore.cs     # Unique constraint race handling
│   │       └── ReportingRepository.cs
│   │
│   └── TicketService.API/              # Minimal API endpoints, middleware
│       ├── Endpoints/
│       │   ├── EventEndpoints.cs
│       │   └── TicketEndpoints.cs
│       ├── Filters/
│       │   └── ValidationFilter.cs     # FluentValidation endpoint filter
│       ├── Middleware/
│       │   └── GlobalExceptionMiddleware.cs
│       ├── appsettings.json            # Docker connection strings
│       ├── appsettings.Development.json # Local dev connection strings
│       └── Program.cs                  # Composition root, rate limiting
│
├── tests/
│   └── TicketService.UnitTests/        # 130 unit tests
│       ├── Domain/
│       ├── Application/
│       └── Infrastructure/
│
├── docker/
│   └── postgres/
│       └── init.sql                    # Creates ticketing_reporting database
├── Dockerfile                          # Multi-stage build
├── docker-compose.yml                  # Single PostgreSQL + migrations + API
└── README.md
```

---

## Future Improvements

### Authentication & Authorisation
- Add JWT Bearer authentication with role-based access control
- Separate roles: `EventManager` (create/update/delete events), `Customer` (purchase tickets), `Admin` (full access)
- Integrate with an identity provider (e.g. Keycloak, Auth0, Azure AD B2C)

### Event Cancellation & Refunds
- Add `PATCH /api/events/{id}/cancel` to cancel an event and trigger automatic refunds
- Add `POST /api/events/{eventId}/tickets/{ticketId}/refund` for individual refund requests
- Integrate with a payment gateway (Stripe, PayPal) for actual money movement

### Waiting List
- When an event sells out, allow customers to join a waiting list
- Automatically offer tickets to the next person on the list when a cancellation occurs

### Notifications
- Publish `TicketPurchased`, `EventCancelled`, `TicketRefunded` events to a message broker (RabbitMQ, Azure Service Bus, Kafka)
- Send confirmation emails via a notification service (SendGrid, AWS SES)

### Integration Tests
- Add `WebApplicationFactory<Program>`-based integration tests using EF Core InMemory or Testcontainers PostgreSQL
- Cover the full HTTP cycle: create event → purchase ticket → check availability → get report
- Test idempotency (same `Idempotency-Key` returns same response), rate limiting (6th purchase returns 429), and concurrent oversell

### Observability
- Add structured logging with Serilog (JSON output for log aggregation)
- Add OpenTelemetry tracing (spans for DB queries, outbox processing)
- Expose Prometheus metrics (`/metrics` endpoint) for request rates, error rates, outbox lag

### Performance
- Add Redis caching for `GET /api/events` and `GET /api/events/{id}` (short TTL, invalidated on write)
- Add a read replica PostgreSQL connection for reporting queries to offload the primary
- Consider moving the `OutboxProcessor` to a dedicated worker service for independent scaling

### Security
- Add HTTPS enforcement and HSTS headers
- Add `Content-Security-Policy`, `X-Frame-Options`, `X-Content-Type-Options` headers
- Rotate database credentials using a secrets manager (HashiCorp Vault, AWS Secrets Manager)
- Add request signing for the idempotency key to prevent key spoofing

### API Versioning
- Add URL-based versioning (`/api/v1/events`, `/api/v2/events`) using `Asp.Versioning.Http`
- Maintain backward compatibility for existing clients during breaking changes

### Deployment
- Add Kubernetes manifests (Deployment, Service, HorizontalPodAutoscaler, PodDisruptionBudget)
- Add a GitHub Actions CI/CD pipeline (build → test → Docker push → deploy)
- Add database migration as a Kubernetes Job (init container pattern)