# Day 28 — Design Review + ADR: Solution

## Design Review

### Strengths

**Domain model earns its complexity.** BookTitle and Loan aggregates enforce real invariants — no duplicate barcodes, no over-lending, overdue detection as a pure value-object predicate. NetArchTest catches layer violations at compile time, not lint time.

**Security posture is production-grade.** Private endpoints on SQL and Key Vault, `publicNetworkAccess: Disabled`, TLS 1.2 enforced, rate limiting validated live (429 fires at request 9), all four ZAP Medium alerts cleared. STRIDE-lite documents 14 threats with applied mitigations.

**Outbox gives honest delivery guarantees.** Domain events write atomically with the business operation. OutboxRelayWorker forwards to Service Bus separately. Notification handlers carry idempotency guards. The system tolerates crashes without losing or duplicating events.

**Infrastructure is fully code-defined.** Bicep modules for VNet, SQL private endpoint, Key Vault network ACLs, App Service VNet integration, and Application Insights. Separate dev/prod parameter files with SKU promotion. No manual portal steps.

**Architecture is enforcement-first.** Four NetArchTest rules prevent domain-to-infrastructure and cross-module dependencies from compiling. Clean Architecture is a structural guarantee, not a naming convention.

### Weaknesses

**Application handlers have zero test coverage.** All eight command handlers — BorrowBook, ReturnBook, PlaceHold, Register, Login, AddBook, AddCopy, TopTitles — are untested. Cross-context coordination lives here.

**Integration tests are excluded from CI.** The workflow explicitly passes `Category!=Integration`. There is no automated proof the system works against a real database or a real Service Bus.

**OutboxRelayWorker has no dead-letter strategy.** Fixed 5-second polling with no backpressure, no exponential backoff on publish failure, and no alerting when message queue depth grows.

**Email delivery is unimplemented.** `INotificationSender` is mocked in every test. No SMTP or Azure Communication Services implementation exists. Notifications are a no-op in every deployed environment.

**Rate limiting does not protect per member.** The partition key is the remote IP. One authenticated member behind a shared IP can deplete another member's quota.

---

## Top Critique

**Critique:**
The domain tests prove aggregate invariants. The architecture tests prove module isolation. Nothing proves that the handlers bridging them work. `BorrowBookHandler` must check copy availability in `CatalogDbContext`, verify member existence in `IdentityDbContext`, and create a Loan in `LendingDbContext`. This is the system's highest-risk code — cross-context coordination across multiple databases — and it has not a single test. The failure mode is silent: the handler compiles, the domain tests pass, the deployment succeeds, but a concurrency bug in the availability check could allow double-booking at runtime with no observable signal until a member arrives to find their reserved book already on loan.

**Why it matters:**
A modular monolith splits domain logic from coordination logic on purpose. Domain tests cover one half of that contract. Leaving the coordination half untested means the system's core business rule — you cannot borrow a copy already on loan — is enforced only by code that has never run under test conditions. That is not a coverage metric. It is an undetected runtime risk in the most consequential path the system executes.

**Design Change:**
Add a TestContainers-based integration test project that boots a real SQL Server container, runs migrations on all five DbContexts, and exercises the full command handler pipeline. The first tests are the three invariant-breaking paths the domain is supposed to prevent: borrow when no copies are available, double-borrow the same physical copy, and place a duplicate hold from the same member. These are the cases where a silent handler bug would produce real-world damage — a library member receiving a book that another member is already holding.

---

## ADR-001

**Status:** Accepted

**Title:** Use Transactional Outbox to Decouple Domain Event Publishing from Service Bus

**Context:**
When BorrowBook, ReturnBook, or PlaceHold completes, domain events (`LoanCreatedDomainEvent`, `LoanReturnedDomainEvent`, `HoldReadyEvent`) must reach the Service Bus so Notifications and Insights can react. Publishing directly inside a command handler creates a dual-write problem: the EF Core commit and the Service Bus publish are not atomic. A crash between them either loses the event or fires it against a transaction that never committed — producing phantom notifications or a silently missing insights update.

**Options Considered:**

*Option A — Direct publish inside command handler:*
Publish to Service Bus before committing the EF Core transaction. Simple, immediate, no extra infrastructure. If the commit fails after a successful publish, the event is orphaned. At-most-once delivery; requires defensive rollback logic in every handler. Each handler acquires an I/O dependency beyond EF Core.

*Option B — Transactional Outbox:*
Write domain events to an `OutboxMessages` table in the same EF Core transaction as the business operation. `OutboxRelayWorker` polls every 5 seconds and forwards pending messages to Service Bus, marking them Dispatched. Atomicity is the database transaction. Reliability is the relay's retry loop. Consumers handle at-least-once delivery via idempotency guards.

**Decision:** Option B — Transactional Outbox, implemented via `OutboxMessages` table and `OutboxRelayWorker` background service.

**Rationale:**
- The SQL transaction is the only atomicity boundary available without a distributed transaction coordinator. Writing `OutboxMessages` in the same transaction means a commit always produces an event and a rollback never does.
- Failure handling is centralised in `OutboxRelayWorker` rather than duplicated across all eight handlers.
- Notification handlers already implement idempotency — verified by two unit tests — making at-least-once delivery safe from day one.
- 5-second polling latency is imperceptible for a library system where borrow confirmation emails have no sub-second SLA.

**Consequences:**
- Added complexity: `OutboxMessages` table, polling overhead on every module's DbContext, and a new background service.
- `OutboxRelayWorker` is a new single point of failure with no dead-letter strategy, no exponential backoff, and no alerting when queue depth grows. This is the most urgent unresolved risk from this decision.
- Latency is eventual, not immediate. A borrow confirmation email may arrive up to 5 seconds after the operation completes.
- The relay worker has no integration tests. A dispatch bug is invisible until it surfaces as message loss in production.

---

## Day-by-Day Build Plan

### Day 29 — Application Handler Coverage

- Add `ShelfLife.Lending.Application.Tests` with TestContainers (SQL Server image)
- Test `BorrowBookHandler`: happy path, copy unavailable → 409, member not found → 404
- Test `ReturnBookHandler`: happy path, already returned → 409
- Test `PlaceHoldHandler`: happy path, duplicate hold from same member → 409
- Establish shared TestContainers DbContext fixture all remaining modules can reuse

### Day 30 — Outbox and Worker Confidence

- Add `OutboxRelayWorker` integration tests using Azurite for Service Bus emulation
- Test: `OutboxMessages` written → relay dispatches → consumer handler invoked end-to-end
- Add exponential backoff (max 5 retries, 2× delay) on publish failure in relay worker
- Add `DeadLetterMessages` table for messages exceeding retry limit; alert on count > 0
- Remove `Category!=Integration` filter from CI; add separate integration test job

### Day 31 — Email Delivery and Auth Hardening

- Implement `INotificationSender` using Azure Communication Services Email SDK
- Store ACS connection string in Key Vault; validate delivery in staging before promoting to prod
- Replace per-IP rate limit with per-member limit (partition key = JWT `sub` claim)
- Set per-member ceiling: 5 borrow requests per hour
- Add integration test for rate limit enforcement using authenticated test tokens

### Day 32 — Observability and Final Hardening

- Add `dotnet list package --vulnerable` to CI; fail build on High/Critical CVEs
- Add `dotnet format --verify-no-changes` to CI to prevent style drift
- Add append-only `AuditLog` table for lending operations (member ID, timestamp, action)
- Run k6 load test against `/api/v1/lending/loans` (50 VU, 2 min); confirm rate limit holds
- Final ZAP baseline scan against staging; confirm FAIL-NEW: 0 before promoting to prod

---

## Final Reflection

Day 28 revealed that ShelfLife is architecturally sound at its edges but hollow in the middle. The domain layer has full test coverage. The infrastructure is security-hardened with live ZAP and rate-limit evidence. But the application layer — the eight handlers that coordinate across bounded contexts — has not a single test between them. This is not a gap that clever architecture fills. It is the part of the system that has to be correct at runtime, and right now it is invisible to every test suite.

The ADR for the Outbox pattern was the right decision. It gives the system honest atomicity guarantees without a distributed transaction coordinator, and its consequences — at-least-once delivery, idempotency requirement — were addressed before they became bugs. But it also introduced `OutboxRelayWorker` as a new single point of failure with no tests, no dead-letter handling, and no alerting. The pattern is correct; the implementation left a risk that the build plan must close.

Days 29–32 are ordered by risk, not effort. Handler coverage first, because a concurrency bug in `BorrowBookHandler` is the most dangerous undetected failure in the system. Outbox confidence second, because a silently-failing relay corrupts the notification and insights streams without any observable signal. Real email third, because a notification subsystem that cannot send is a feature in name only. Hardening last, because dependency scanning and audit logs matter for compliance but not for correctness. By Day 32, every path the system executes should be backed by a test that would catch the most obvious way it could go wrong.
