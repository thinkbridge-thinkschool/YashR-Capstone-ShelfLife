using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using ShelfLife.Lending.Infrastructure;
using ShelfLife.OverdueWorker;

var builder = Host.CreateApplicationBuilder(args);

// ── Serilog ───────────────────────────────────────────────────────────────────
builder.Services.AddSerilog(cfg => cfg
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ── OpenTelemetry → Azure Monitor ─────────────────────────────────────────────
// Only register Azure Monitor exporter when the connection string is present.
// In local dev (Docker) the env var is absent, so skip it to avoid a crash.
var aiConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ShelfLife.OverdueWorker"))
    .WithTracing(t => t.AddSource(OverdueReminderWorker.ActivitySource.Name));

if (!string.IsNullOrEmpty(aiConnectionString))
    otel.WithTracing(t => t.AddAzureMonitorTraceExporter());

// ── Minimal Lending registration ──────────────────────────────────────────────
// The worker only needs LendingDbContext + ILoanRepository to find overdue loans
// and write outbox messages.  AddLendingModule also registers application
// handlers (BorrowBookHandler etc.) that require IBookTitleRepository from the
// Catalog module — which is not available here, causing DI validation to fail.
builder.Services.AddDbContext<LendingDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("ShelfLife"))
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddScoped<ShelfLife.Lending.Domain.ILoanRepository, LoanRepository>();

// Worker only uses LendingDbContext, so IUnitOfWork delegates straight to it.
builder.Services.AddScoped<ShelfLife.SharedKernel.IUnitOfWork>(sp =>
    (ShelfLife.SharedKernel.IUnitOfWork)sp.GetRequiredService<LendingDbContext>());

builder.Services.AddHostedService<OverdueReminderWorker>();

var host = builder.Build();
host.Run();
