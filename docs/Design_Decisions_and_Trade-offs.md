# Design Decisions and Trade-offs

This document summarises the critical architectural choices made in the Ticket Service and the trade-offs accepted.

---

## 1. Clean Architecture

The service is split into four projects (Domain, Application, Infrastructure, API) with strict dependency rules. This keeps business logic testable in isolation and prevents domain rules from being bypassed by infrastructure code. The trade-off is more boilerplate than a simple CRUD service.

---

## 2. Pessimistic Locking for Ticket Purchases

Purchases use a single atomic `UPDATE ... WHERE available_quantity >= $1 RETURNING ...` statement. The check and decrement happen under an exclusive PostgreSQL row lock, making overselling impossible under concurrent load. The trade-off is that all concurrent purchases for the same tier serialise through that lock, which becomes a bottleneck at extreme throughput.

---

## 3. Optimistic Concurrency for Event Management

Event update operations use PostgreSQL's `xmin` system column as an EF Core concurrency token. Conflicts return HTTP 409 and the client retries. This avoids holding locks during application processing, which is appropriate for low-contention admin operations.

---

## 4. Dual-Database CQRS (Write DB + Reporting DB)

Transactional writes go to a normalised `ticketing` database; reporting queries read from a denormalised `ticketing_reporting` database with pre-aggregated summaries. This keeps reporting fast without expensive aggregations on the write side. The trade-off is eventual consistency — the reporting DB lags by up to one outbox poll cycle (~5 s).

---

## 5. Transactional Outbox Pattern

When a ticket is purchased, an `OutboxMessage` is inserted in the same database transaction as the ticket record. A background processor then applies these events to the Reporting DB. This guarantees atomicity without an external message broker. The known trade-off is at-least-once delivery — a crash between the reporting write and marking the message processed would cause double-counting.

---

## 6. Database-Backed Idempotency Keys

An optional `Idempotency-Key` header allows clients to safely retry purchase requests. The key and response are stored in the database for 24 hours, so the guarantee survives API restarts. The trade-off is that clients who omit the header have no duplicate-purchase protection on retry.

---

## 7. Encapsulated Domain Entities

All entities use private setters and `static Create(...)` factory methods. State changes go through named behaviour methods (`Cancel()`, `Refund()`) that enforce invariants before mutating state. This prevents business rules from being bypassed by application code, at the cost of some EF Core friction (reflection-based materialisation).

---

## 8. Three Differentiated Rate-Limiting Policies

Read, write, and purchase endpoints each have their own rate-limiting policy. The purchase endpoint uses a sliding-window algorithm (5 req / 60 s per IP) to prevent boundary bursts from scalping bots. The trade-off is that IP-based limits are bypassable via NAT or rotating proxies, and in-memory counters are not shared across multiple API instances.

---

## 9. RFC 7807 Problem Details for All Errors

All error responses use `application/problem+json` with consistent `status`, `title`, `detail`, and `instance` fields. Domain rule violations map to HTTP 409, validation failures to 422, and missing resources to 404. Internal details are never sent to clients.

---

## 10. Transactional Outbox: Adaptive Polling + Dead-Letter Handling

The outbox processor polls every 5 s when active and backs off to 30 s when idle, reducing unnecessary database load. Messages that fail 5 times are dead-lettered (logged at `Critical` and excluded from future processing) rather than deleted, preserving them for manual replay. The trade-off is that dead-lettered messages accumulate in the `outbox_messages` table with no automatic cleanup.