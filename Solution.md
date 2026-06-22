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

## Top Critique and How It Changed the Design

**Critique:**
The domain tests prove aggregate invariants. The architecture tests prove module isolation. Nothing proves that the handlers bridging them work. `BorrowBookHandler` must check copy availability in `CatalogDbContext`, verify member existence in `IdentityDbContext`, and create a Loan in `LendingDbContext`. This is the system's highest-risk code — cross-context coordination across multiple databases — and it has not a single test. The failure mode is silent: the handler compiles, the domain tests pass, the deployment succeeds, but a concurrency bug in the availability check could allow double-booking at runtime with no observable signal until a member arrives to find their reserved book already on loan.

**Why it matters:**
A modular monolith splits domain logic from coordination logic on purpose. Domain tests cover one half of that contract. Leaving the coordination half untested means the system's core business rule — you cannot borrow a copy already on loan — is enforced only by code that has never run under test conditions. That is not a coverage metric. It is an undetected runtime risk in the most consequential path the system executes.

**How it changed the design:**
The critique directly shaped the Day 29–32 build plan ordering. Originally the plan addressed outbox hardening first because it felt like the more architectural problem. After this critique, application handler coverage moves to Day 29 — before outbox, before email, before anything else — because a concurrency bug in `BorrowBookHandler` is more dangerous than a missing dead-letter queue. A silently-failing outbox delays a notification email. A silently-failing availability check allows a member to borrow a book that does not exist or is already on loan.

The plan also changes the test target: rather than unit tests with mocked repositories, the first test suite uses Testcontainers with a real SQL Server container and real DbContext instances — because the entire failure mode is about cross-context coordination that mocks cannot replicate. The critique did not change the architecture; it changed what gets tested first and why.

---

## ADR-001: Use Transactional Outbox to Decouple Domain Event Publishing from Service Bus

**Status:** Accepted

**Context:**
When BorrowBook, ReturnBook, or PlaceHold completes, domain events (`LoanCreatedDomainEvent`, `LoanReturnedDomainEvent`, `HoldReadyEvent`) must reach the Service Bus so Notifications and Insights can react. Publishing directly inside a command handler creates a dual-write problem: the EF Core commit and the Service Bus publish are not atomic. A crash between them either loses the event or fires it against a transaction that never committed — producing phantom notifications or a silently missing insights update.

**Options Considered:**

*Option A — Direct publish inside command handler:*
Publish to Service Bus before committing the EF Core transaction. Simple, immediate, no extra infrastructure. If the commit fails after a successful publish, the event is orphaned. At-most-once delivery; requires defensive rollback logic in every handler. Each handler acquires an I/O dependency beyond EF Core.

*Option B — Transactional Outbox:*
Write domain events to an `OutboxMessages` table in the same EF Core transaction as the business operation. `OutboxRelayWorker` polls every 5 seconds and forwards pending messages to Service Bus, marking them Dispatched. Atomicity is the database transaction. Reliability is the relay's retry loop. Consumers handle at-least-once delivery via idempotency guards.

**Decision:** Option B — Transactional Outbox, implemented via `OutboxMessages` table and `OutboxRelayWorker` background service.

**Mechanism:**
When a command handler calls `SaveChangesAsync()`, the `ShelfLifeDbContext` override intercepts the save, serializes each pending domain event from `AggregateRoot._domainEvents` into a JSON payload, and writes one `OutboxMessages` row per event — in the same database transaction as the business operation. The transaction either commits both the business entity change and the outbox row together, or rolls both back. There is no state where one succeeds without the other.

`OutboxRelayWorker` (a .NET `BackgroundService`) wakes every 5 seconds, queries for rows where `ProcessedAt IS NULL`, and for each row: deserializes the payload, publishes to the Azure Service Bus topic named in `TopicName`, then marks the row `ProcessedAt = UtcNow`. On publish failure it stores the error in the `Error` column and increments `RetryCount`. The worker loops until the application shuts down.

Consumer handlers (`BookBorrowedNotificationHandler`, `HoldReadyNotificationHandler`, `LoanOverdueNotificationHandler`) each call `IIdempotencyService.HasBeenProcessedAsync(eventId)` before acting, then mark the event processed after sending. This guards against the at-least-once delivery guarantee producing a duplicate notification.

**Why this decision over Option A:**
ShelfLife has eight command handlers coordinating across five separate DbContexts (Catalog, Lending, Identity, Insights, Notifications). With direct publish, every handler would need its own try/catch: publish first, and if the EF Core commit then fails, attempt a compensating delete or accept the orphaned event. That logic would be duplicated eight times, would be untested, and would silently fail differently in each handler depending on which DbContext was mid-transaction. The Outbox centralises that reliability contract in one place — `OutboxRelayWorker` — so every handler gets the same atomicity guarantee without any handler knowing Service Bus exists.

**Consequences:**

*Positive (current):*
- Every committed business operation reliably produces the corresponding integration events — no handler can accidentally skip publication
- Command handlers have no direct Service Bus dependency; they are testable without a live broker
- Failure and retry logic is owned in one place

*Negative (current — open risks):*
- `OutboxRelayWorker` is a new single point of failure: if it crashes and does not restart, events queue indefinitely with no alerting
- No dead-letter strategy — messages that repeatedly fail to publish have no escalation path and will block indefinitely
- No exponential backoff — the relay retries at a fixed 5-second cadence regardless of failure type or error severity
- No integration tests — a dispatch bug is invisible until it surfaces as silent message loss in production

*After Days 29–30 address the open risks:*
- Exponential backoff (max 5 retries, 2× delay) removes the fixed-cadence hammering on a broken broker connection
- A `DeadLetterMessages` table with an Application Insights alert on count > 0 means a stuck message surfaces as an observable incident, not a silent gap in the notifications stream
- Integration tests covering the full path (OutboxMessages row written → relay dispatches → consumer handler invoked) mean a dispatch bug fails CI before it reaches production
- Once these three are in place, the Outbox pattern delivers on its guarantee end to end — not just at the write side, but through to confirmed consumer receipt

---

## Day-by-Day Build Plan (Day 22 – Day 32)

### Day 22 ✅ — Capstone Foundation
- Scaffolded 5-module modular monolith: Catalog, Lending, Identity, Insights, Notifications
- Applied Clean Architecture per module: Domain / Application / Contracts / Infrastructure
- Built SharedKernel: `AggregateRoot<T>`, `ValueObject`, `IDomainEvent`, `IUnitOfWork`
- Implemented `BookTitle` aggregate with `Copy` entity — no duplicate barcodes, copy-state enforcement
- Implemented `Loan` aggregate with `Hold` entity — overdue detection via `LoanPeriod` value object
- Implemented `Isbn` value object using `Span<T>` + `stackalloc` — zero heap allocation ISBN-10/13 validation
- Built Identity module: `Member` aggregate, `JwtService`, `PasswordHasher`, Register/Login commands
- Built `OverdueReminderWorker` BackgroundService — polls for overdue loans and publishes events
- Wired EF Core 9 per-module DbContexts inheriting `ShelfLifeDbContext`
- Built Transactional Outbox: `OutboxMessages` table written in same EF Core transaction; `OutboxRelayWorker` BackgroundService forwards to Azure Service Bus
- Added Polly (`Microsoft.Extensions.Http.Resilience`) on ISBN enrichment HTTP client
- Added NetArchTest architecture enforcement rules — no cross-module deps, no domain → infra refs
- Added CI with GitHub Actions — build + 22 unit and architecture tests green

### Day 23 ✅ — Bicep IaC
- Authored parameterized Bicep modules: VNet, SQL Server, Azure Service Bus, App Service, Key Vault, Application Insights
- Added private endpoint for SQL Server — `publicNetworkAccess: Disabled`, traffic never leaves the VNet
- Enforced TLS 1.2 minimum on SQL Server (`minimalTlsVersion: '1.2'`)
- Added Key Vault network ACLs — only App Service subnet allowed
- Added App Service VNet integration — outbound traffic routed through private subnet
- Created separate dev/prod parameter files with SKU promotion (Basic dev → Standard prod)
- Verified `az deployment group create` deploys cleanly against both parameter sets

### Day 24 ✅ — Deployment Stacks + azd CLI
- Replaced raw `az deployment` with `az stack group create` — stack tracks all resources as a single unit, prevents orphaned resources on teardown
- Added `azure.yaml` for `azd` CLI — `azd up` provisions infrastructure and deploys API in one command
- Added deployment scripts for dev and prod environments
- Cost optimised prod SKUs — downgraded where the capstone does not need production-grade sizing

### Day 25 ✅ — Managed Identity and Zero-Secret Configuration
- Removed all connection strings and secrets from `appsettings.json`
- Wired `DefaultAzureCredential` for SQL, Service Bus, and Key Vault access in both API and `OverdueWorker`
- Added Key Vault reference for `APPLICATIONINSIGHTS_CONNECTION_STRING` — App Service resolves it at runtime via Managed Identity
- Configured Entra ID (`Microsoft.Identity.Web`) for production JWT validation — no shared secret in config
- Confirmed end-to-end: API running in Azure with zero hardcoded secrets in any config file

### Day 26 ✅ — Observability (App Insights + OpenTelemetry + KQL)
- Wired `Azure.Monitor.OpenTelemetry.AspNetCore` in API — traces flow to Application Insights
- Wired `Azure.Monitor.OpenTelemetry.Exporter` in `OverdueWorker` — worker traces appear in the same Application Insights instance
- Added `Serilog.AspNetCore` with `Enrich.FromLogContext()` and Seq local sink for development
- Added `AddSource("Azure.Messaging.ServiceBus")` — Service Bus publish/receive spans appear in distributed traces
- Wrote and verified KQL queries: error rate by endpoint, p50/p99 latency, worker trace confirmation
- Confirmed distributed trace end-to-end: HTTP request → command handler → outbox write → worker dispatch → consumer handler

### Day 27 ✅ — Security Hardening
- Authored STRIDE-lite threat model: 14 threats across Spoofing, Tampering, Repudiation, Information Disclosure, DoS, Elevation — each with applied mitigation
- Added Kestrel hardening: `AddServerHeader = false` (removes version fingerprinting), `MaxRequestBodySize = 65,536` bytes (prevents memory-pressure DoS)
- Added security headers middleware: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, environment-split CSP
- Added fixed-window rate limiting: `identity` policy (10 req/min on `/register` and `/login`), `api` policy (60 req/min on all authenticated endpoints)
- Added OpenAPI with JWT Bearer security scheme — Swagger UI shows padlock on every authenticated endpoint
- Ran OWASP ZAP baseline scan against live staging URL — resolved all four Medium alerts; confirmed `FAIL-NEW: 0`

### Day 28 ✅ — Design Review + ADR
- Conducted full design review: 5 strengths, 5 weaknesses, identified untested application handlers as highest-risk gap
- Wrote ADR-001 (Transactional Outbox) — context, mechanism, options, rationale, consequences
- Cross-referenced full curriculum (Days 1–32) against ShelfLife — identified 16 gaps, 30 technologies present
- Produced Day 29–32 build plan ordered by risk

### Day 29 — Application Handler Coverage
- Add `ShelfLife.Lending.Application.Tests` with Testcontainers (SQL Server image)
- Test `BorrowBookHandler`: happy path, copy unavailable → 409, member not found → 404
- Test `ReturnBookHandler`: happy path, already returned → 409
- Test `PlaceHoldHandler`: happy path, duplicate hold from same member → 409
- Establish shared Testcontainers DbContext fixture all remaining modules can reuse
- Wire `IHybridCache` into `BorrowBookHandler` for book availability lookup — `Microsoft.Extensions.Caching.Hybrid` 9.3.0 is already imported in `LendingInfrastructure.csproj`, registration and one cache call is all that remains

### Day 30 — Outbox and Worker Confidence
- Add `OutboxRelayWorker` integration tests using Azurite for Service Bus emulation
- Test: `OutboxMessages` written → relay dispatches → consumer handler invoked end-to-end
- Add exponential backoff (max 5 retries, 2× delay) on publish failure in relay worker
- Add `DeadLetterMessages` table for messages exceeding retry limit; alert on count > 0
- Remove `Category!=Integration` filter from CI; add separate integration test job

### Day 31 — Email Delivery and Auth Hardening
- Implement `INotificationSender` using Azure Communication Services Email SDK
- Store ACS connection string in Key Vault; validate delivery in staging before promoting to prod
- Replace per-IP rate limit with per-member limit (partition key = JWT `sub` claim); set ceiling at 5 borrow requests per hour
- Replace `Results.BadRequest("error string")` with RFC 9457 `ProblemDetails` across all endpoints — one `builder.Services.AddProblemDetails()` registration plus updated error returns

### Day 32 — Observability and Final Hardening
- Add `dotnet list package --vulnerable` to CI; fail build on High/Critical CVEs
- Add `dotnet format --verify-no-changes` to CI to prevent style drift
- Add append-only `AuditLog` table for lending operations (member ID, timestamp, action)
- Run k6 load test against `/api/v1/lending/loans` (50 VU, 2 min); confirm rate limit holds
- Final ZAP baseline scan against staging; confirm `FAIL-NEW: 0` before promoting to prod

---

## Final Reflection

Day 28 revealed that ShelfLife is architecturally sound at its edges but hollow in the middle. The domain layer has full test coverage. The infrastructure is security-hardened with live ZAP and rate-limit evidence. But the application layer — the eight handlers that coordinate across bounded contexts — has not a single test between them. This is not a gap that clever architecture fills. It is the part of the system that has to be correct at runtime, and right now it is invisible to every test suite.

The ADR for the Outbox pattern was the right decision. It gives the system honest atomicity guarantees without a distributed transaction coordinator, and its consequences — at-least-once delivery, idempotency requirement — were addressed before they became bugs. But it also introduced `OutboxRelayWorker` as a new single point of failure with no tests, no dead-letter handling, and no alerting. The pattern is correct; the implementation left a risk that the build plan must close.

Days 29–32 are ordered by risk, not effort. Handler coverage first, because a concurrency bug in `BorrowBookHandler` is the most dangerous undetected failure in the system. Outbox confidence second, because a silently-failing relay corrupts the notification and insights streams without any observable signal. Real email third, because a notification subsystem that cannot send is a feature in name only. Hardening last, because dependency scanning and audit logs matter for compliance but not for correctness. By Day 32, every path the system executes should be backed by a test that would catch the most obvious way it could go wrong.
