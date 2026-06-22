# ADR-001: Use Transactional Outbox to Decouple Domain Event Publishing from Service Bus

## Status

Accepted

---

## Context

When BorrowBook, ReturnBook, or PlaceHold completes, domain events (`LoanCreatedDomainEvent`, `LoanReturnedDomainEvent`, `HoldReadyEvent`) must reach the Service Bus so Notifications and Insights can react.

Publishing directly inside a command handler creates a dual-write problem: the EF Core commit and the Service Bus publish are not atomic. A crash between them either loses the event or fires it against a transaction that never committed — producing phantom notifications or a silently missing insights update.

The system needed a reliable way to guarantee that every committed business operation produces exactly the downstream events it should, with no data loss and no phantom events.

---

## Options Considered

### Option A — Direct publish inside command handler

Publish to Service Bus before committing the EF Core transaction.

**Advantages:**
- Simpler implementation — no additional infrastructure
- Immediate event delivery to consumers

**Disadvantages:**
- If the EF Core commit fails after a successful publish, the event is orphaned — downstream consumers receive an event for data that does not exist
- At-most-once delivery with no retry mechanism
- Defensive rollback logic must be duplicated across every command handler
- Each handler acquires a direct I/O dependency on Service Bus beyond EF Core

### Option B — Transactional Outbox

Write domain events to an `OutboxMessages` table as part of the same EF Core transaction as the business operation. A background worker (`OutboxRelayWorker`) polls for pending messages and forwards them to Service Bus, marking each one Dispatched on success.

**Advantages:**
- Atomicity guaranteed by the database transaction — a commit always produces an event, a rollback never does
- Failure handling centralised in `OutboxRelayWorker`, not duplicated across handlers
- At-least-once delivery via the relay's retry loop
- Consumers can guard against duplicates with idempotency checks

**Disadvantages:**
- Additional infrastructure: `OutboxMessages` table, polling overhead, background service
- Eventual consistency — events are not delivered synchronously with the operation
- `OutboxRelayWorker` becomes a new operational dependency

---

## Decision

**Option B — Transactional Outbox**, implemented via an `OutboxMessages` table written in the same EF Core transaction and an `OutboxRelayWorker` background service that forwards pending messages to Azure Service Bus.

---

## Mechanism

When a command handler calls `SaveChangesAsync()`, the `ShelfLifeDbContext` override intercepts the save, serializes each pending domain event from `AggregateRoot._domainEvents` into a JSON payload, and writes one `OutboxMessages` row per event — in the same database transaction as the business operation. The transaction either commits both the business entity change and the outbox row together, or rolls both back. There is no state where one succeeds without the other.

`OutboxRelayWorker` (a .NET `BackgroundService`) wakes every 5 seconds, queries for rows where `ProcessedAt IS NULL`, and for each row: deserializes the payload, publishes to the Azure Service Bus topic named in `TopicName`, then marks the row `ProcessedAt = UtcNow`. On publish failure it stores the error in the `Error` column and increments `RetryCount`. The worker loops until the application shuts down.

Consumer handlers (`BookBorrowedNotificationHandler`, `HoldReadyNotificationHandler`, `LoanOverdueNotificationHandler`) each call `IIdempotencyService.HasBeenProcessedAsync(eventId)` before acting, then mark the event processed after sending. This guards against the at-least-once delivery guarantee producing a duplicate notification.

---

## Why This Decision Over Option A

ShelfLife has eight command handlers coordinating across five separate DbContexts (Catalog, Lending, Identity, Insights, Notifications). With direct publish, every handler would need its own try/catch: publish first, and if the EF Core commit then fails, attempt a compensating delete or accept the orphaned event. That logic would be duplicated eight times, would be untested, and would silently fail differently in each handler depending on which DbContext was mid-transaction. The Outbox centralises that reliability contract in one place — `OutboxRelayWorker` — so every handler gets the same atomicity guarantee without any handler knowing Service Bus exists.

---

## Rationale

- The SQL transaction is the only atomicity boundary available without introducing a distributed transaction coordinator. Writing `OutboxMessages` in the same transaction as the business operation means a commit always produces an event and a rollback never does.
- Reliability concerns are centralised in `OutboxRelayWorker` rather than duplicated across all eight command handlers.
- Notification handlers already implement idempotency guards — verified by unit tests — making at-least-once delivery safe from day one.
- A 5-second polling interval is imperceptible for a library system where borrow confirmation emails have no sub-second SLA.

---

## Consequences

### Positive

- Every committed business operation reliably produces the corresponding integration events — no handler can accidentally skip publication
- Command handlers have no direct Service Bus dependency; they are testable without a live broker
- Failure and retry logic is owned in one place

### Negative (Open Risks)

- `OutboxRelayWorker` is a new single point of failure: if it crashes and does not restart, events queue indefinitely with no alerting
- No dead-letter strategy — messages that repeatedly fail to publish have no escalation path and will block indefinitely
- No exponential backoff — the relay retries at a fixed 5-second cadence regardless of failure type or error severity
- No integration tests — a dispatch bug is invisible until it surfaces as silent message loss in production
- Latency is eventual: a borrow confirmation email may arrive up to 5 seconds after the operation completes

### After Days 29–30 Address the Open Risks

- Exponential backoff (max 5 retries, 2× delay) removes the fixed-cadence hammering on a broken broker connection
- A `DeadLetterMessages` table with an Application Insights alert on count > 0 means a stuck message surfaces as an observable incident, not a silent gap in the notifications stream
- Integration tests covering the full path (OutboxMessages row written → relay dispatches → consumer handler invoked) mean a dispatch bug fails CI before it reaches production
- Once these three are in place, the Outbox pattern delivers on its guarantee end to end — not just at the write side, but through to confirmed consumer receipt
