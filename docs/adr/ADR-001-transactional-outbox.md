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

## Rationale

- The SQL transaction is the only atomicity boundary available without introducing a distributed transaction coordinator. Writing `OutboxMessages` in the same transaction as the business operation means a commit always produces an event and a rollback never does.
- Reliability concerns are centralised in `OutboxRelayWorker` rather than duplicated across all eight command handlers.
- Notification handlers already implement idempotency guards — verified by unit tests — making at-least-once delivery safe from day one.
- A 5-second polling interval is imperceptible for a library system where borrow confirmation emails have no sub-second SLA.

---

## Consequences

### Positive

- Every committed business operation reliably produces the corresponding integration events
- Command handlers remain free of direct Service Bus dependencies
- Failure and retry logic is owned in one place

### Negative

- `OutboxRelayWorker` is a new single point of failure: if it crashes and does not restart, events queue indefinitely with no alerting
- No dead-letter strategy — messages that repeatedly fail to publish have no escalation path
- No exponential backoff — the relay retries at a fixed 5-second cadence regardless of failure type
- The relay worker currently has no integration tests; a dispatch bug is invisible until it surfaces as message loss in production
- Latency is eventual: a borrow confirmation email may arrive up to 5 seconds after the operation completes

### Risks to Address (Days 29–30)

- Add exponential backoff (max 5 retries, 2× delay) to `OutboxRelayWorker`
- Add a `DeadLetterMessages` table for messages that exceed the retry limit
- Add an Application Insights alert when dead-letter count exceeds zero
- Add integration tests for the full outbox → Service Bus → consumer handler path using Azurite
