# Day 22 — Capstone Kickoff: Design + Scaffold

**Product:** ShelfLife — Library Management System  
**Pattern:** Modular Monolith · Clean Architecture · Domain-Driven Design  
**Stack:** .NET 10 · ASP.NET Core 10 · EF Core 9 · Azure Service Bus · xUnit

---

## One-Page Design

### Bounded Contexts (5 Modules)

| Module | Responsibility |
|---|---|
| **Identity** | Member registration, JWT auth, role assignment (Member / Librarian) |
| **Catalog** | BookTitle aggregate, physical copies, ISBN enrichment via Open Library API |
| **Lending** | Borrow / return / hold workflow, overdue detection |
| **Notifications** | Email alerts for borrow confirmation, hold-ready, and overdue reminders |
| **Insights** | Read-model projections for librarian dashboards (top titles, overdue counts) |

### Core Aggregate — `BookTitle`

```
BookTitle (AggregateRoot<Guid>)
 ├── Isbn            : ValueObject   — validates ISBN-10/13, strips hyphens
 ├── CopyBarcode     : ValueObject   — non-empty barcode string
 ├── List<Copy>      : child entity  — CopyStatus { Available | OnLoan | Lost }
 └── BookTitleStatus : Available | FullyOnLoan | Unavailable  (derived, refreshed on every mutation)

Methods
 Create(id, isbn, title, author, year)  → raises BookTitleCreatedEvent
 AddCopy(copyId, barcode)               → guards duplicate barcode, raises CopyAddedEvent
 LoanCopy(copyId, loanId)               → delegates to Copy.Loan(), refreshes status
 ReturnCopy(copyId)                     → delegates to Copy.Return(), refreshes status
 MarkCopyLost(copyId)                   → raises CopyMarkedLostEvent
```

### 4 Async Flows

| Flow | Trigger | Integration Event | Consumer |
|---|---|---|---|
| **Borrow confirmation** | `BorrowBook` command succeeds | `BookBorrowedEvent` → Service Bus | Notifications |
| **Hold ready** | `ReturnBook` with pending hold | `HoldReadyEvent` → Service Bus | Notifications |
| **Overdue reminder** | `OverdueReminderWorker` (24h poll) | `LoanOverdueEvent` → Service Bus | Notifications |
| **Insights projection** | Any Lending integration event | Internal read-model update | Insights |

---

## Solution Structure

```
ShelfLife/
├── ShelfLife.slnx                          # .NET 10 solution file
├── Directory.Build.props                   # net10.0, nullable, TreatWarningsAsErrors
├── docker-compose.yml                      # SQL Server, Azurite, Seq, API, Worker
├── azure.yaml                              # azd — Container Apps + SWA
│
├── src/
│   ├── Host/
│   │   └── ShelfLife.Api/                  # ASP.NET Core minimal-API host
│   │       ├── Program.cs                  # Serilog, OpenTelemetry, JWT, all modules wired
│   │       └── Endpoints/
│   │           ├── IdentityEndpoints.cs
│   │           ├── CatalogEndpoints.cs
│   │           ├── LendingEndpoints.cs
│   │           └── InsightsEndpoints.cs
│   │
│   ├── Modules/
│   │   ├── Catalog/
│   │   │   ├── ShelfLife.Catalog.Domain/       # BookTitle, Copy, Isbn, CopyBarcode, domain events
│   │   │   ├── ShelfLife.Catalog.Application/  # AddBookByIsbnHandler, AddCopyHandler, ISBN enrichment
│   │   │   ├── ShelfLife.Catalog.Contracts/    # Integration events (outbound)
│   │   │   └── ShelfLife.Catalog.Infrastructure/ # CatalogDbContext, BookTitleRepository, CatalogModule
│   │   │
│   │   ├── Lending/
│   │   │   ├── ShelfLife.Lending.Domain/       # Loan, Hold, LoanPeriod, domain events
│   │   │   ├── ShelfLife.Lending.Application/  # BorrowBookHandler, ReturnBookHandler, PlaceHoldHandler
│   │   │   ├── ShelfLife.Lending.Contracts/    # Integration events (BookBorrowedEvent, HoldReadyEvent …)
│   │   │   └── ShelfLife.Lending.Infrastructure/ # LendingDbContext, LoanRepository, LendingModule
│   │   │
│   │   ├── Identity/
│   │   │   ├── ShelfLife.Identity.Domain/
│   │   │   ├── ShelfLife.Identity.Application/
│   │   │   ├── ShelfLife.Identity.Contracts/
│   │   │   └── ShelfLife.Identity.Infrastructure/
│   │   │
│   │   ├── Insights/
│   │   │   ├── ShelfLife.Insights.Application/
│   │   │   ├── ShelfLife.Insights.Contracts/
│   │   │   └── ShelfLife.Insights.Infrastructure/
│   │   │
│   │   └── Notifications/
│   │       ├── ShelfLife.Notifications.Application/  # Event handlers + idempotency guard
│   │       ├── ShelfLife.Notifications.Contracts/
│   │       └── ShelfLife.Notifications.Infrastructure/
│   │
│   └── Shared/
│       ├── ShelfLife.SharedKernel/             # Entity, AggregateRoot, ValueObject, IDomainEvent,
│       │                                       # IUnitOfWork, IMessageConsumer<T>, Result, PagedList
│       ├── ShelfLife.Infrastructure.Messaging/ # IMessagePublisher, ServiceBusPublisher
│       ├── ShelfLife.Infrastructure.Outbox/    # OutboxMessage, IOutboxStore, OutboxRelayWorker
│       └── ShelfLife.Infrastructure.Persistence/ # ShelfLifeDbContext (abstract base, owns OutboxMessages)
│
├── Workers/
│   └── ShelfLife.OverdueWorker/            # 24-hour BackgroundService, publishes LoanOverdueEvent
│
└── tests/
    ├── unit/
    │   ├── ShelfLife.Catalog.Domain.Tests/         # 8 tests — BookTitle invariants, events, ISBN
    │   ├── ShelfLife.Lending.Domain.Tests/         # 8 tests — Loan lifecycle, holds, overdue
    │   └── ShelfLife.Notifications.Application.Tests/ # 2 tests — idempotency guard
    └── arch/
        └── ShelfLife.Architecture.Tests/           # 4 tests — NetArchTest layer-dependency rules
```

---

## Key Technical Decisions

### Clean Architecture Layer Rules (enforced by NetArchTest)
- Domain has **no** dependency on Application or Infrastructure
- Application has **no** dependency on Infrastructure
- Lending Domain has **no** dependency on Catalog Infrastructure (module isolation)

### `IMessageConsumer<T>` lives in SharedKernel
Application layers implement the messaging contract without referencing any Infrastructure package. The interface is:
```csharp
public interface IMessageConsumer<T> where T : class
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
```

### Outbox Pattern
Every domain command runs in a single EF transaction:
1. Save domain state
2. Write `OutboxMessage` row to the same DB
3. `OutboxRelayWorker` (polls every 5 s) picks pending rows and publishes to Azure Service Bus

### Idempotent Notification Handlers
Each handler checks `IIdempotencyStore.HasBeenProcessedAsync(eventId)` before sending.  
Duplicate messages (Service Bus at-least-once) are silently dropped.

### Polly Resilience (Catalog ISBN lookup)
`AddStandardResilienceHandler()` on the Open Library `HttpClient`:  
retry with exponential back-off → circuit breaker → timeout.

---

## API Endpoints

| Method | Route | Auth | Handler |
|---|---|---|---|
| POST | `/api/identity/register` | — | Register member |
| POST | `/api/identity/login` | — | Return JWT |
| POST | `/api/catalog/books` | Librarian | Add book by ISBN |
| POST | `/api/catalog/books/{id}/copies` | Librarian | Add physical copy |
| POST | `/api/lending/borrow` | Member | Borrow a copy |
| POST | `/api/lending/return` | Member | Return a copy |
| POST | `/api/lending/holds` | Member | Place a hold |
| GET | `/api/insights/top-titles` | Librarian | Most borrowed titles |

---

## Test Results

```
dotnet test ShelfLife.slnx

Passed!  - Failed: 0, Passed:  8, Total:  8  — ShelfLife.Catalog.Domain.Tests
Passed!  - Failed: 0, Passed:  8, Total:  8  — ShelfLife.Lending.Domain.Tests
Passed!  - Failed: 0, Passed:  2, Total:  2  — ShelfLife.Notifications.Application.Tests
Passed!  - Failed: 0, Passed:  4, Total:  4  — ShelfLife.Architecture.Tests

Total: 22 passed, 0 failed
```

### What each test suite covers

**Catalog Domain (8)**
- `Create_RaisesBookTitleCreatedEvent`
- `AddCopy_IncreasesAvailability`
- `AddCopy_DuplicateBarcode_Throws`
- `LoanCopy_SetsStatusToOnLoan`
- `ReturnCopy_ResetsStatusToAvailable`
- `LoanCopy_WhenAlreadyOnLoan_Throws`
- `Isbn_InvalidFormat_Throws`
- `Isbn_NormalisesHyphens`

**Lending Domain (8)**
- `Create_RaisesLoanCreatedDomainEvent`
- `Return_SetsStatusToReturned`
- `Return_RaisesLoanReturnedDomainEvent`
- `Return_WithPendingHold_RaisesHoldReadyEvent`
- `Return_WhenAlreadyReturned_Throws`
- `PlaceHold_AddsHoldToCollection`
- `PlaceHold_DuplicateMember_Throws`
- `LoanPeriod_IsOverdue_WhenPastDueDate`

**Notifications Idempotency (2)**
- `BookBorrowedHandler_WhenAlreadyProcessed_DoesNotSend`
- `BookBorrowedHandler_WhenNew_SendsAndMarksProcessed`

**Architecture (4)**
- Domain should not reference Application
- Domain should not reference Infrastructure
- Application should not reference Infrastructure
- Lending Domain should not reference Catalog Infrastructure

---

## Build

```bash
dotnet build ShelfLife.slnx   # 0 errors, 0 warnings
dotnet test  ShelfLife.slnx   # 22/22 passed
```

## Local Dev (Docker)

```bash
docker compose up -d   # SQL Server :1433 · Azurite :10000 · Seq :5341
dotnet run --project src/Host/ShelfLife.Api
# Swagger UI → http://localhost:8080/swagger
```

---

## Packages Used

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | 9.0.5 | ORM per-module DbContexts |
| `Microsoft.Extensions.Caching.Hybrid` | 9.3.0 | L1+L2 HybridCache |
| `Microsoft.Extensions.Http.Resilience` | 9.0.0 | Polly standard pipeline on HttpClient |
| `Azure.Messaging.ServiceBus` | 7.18.4 | Async message bus |
| `Serilog.AspNetCore` | 8.0.3 | Structured logging |
| `OpenTelemetry.Extensions.Hosting` | 1.9.0 | Distributed tracing |
| `Swashbuckle.AspNetCore` | 7.3.1 | Swagger / OpenAPI |
| `xunit` | 2.9.3 | Unit tests |
| `FluentAssertions` | 7.2.0 | Readable assertions |
| `NSubstitute` | 5.3.0 | Mocking |
| `NetArchTest.Rules` | 1.3.2 | Architecture enforcement |
