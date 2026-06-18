using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using ShelfLife.Infrastructure.Messaging;
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
// AddAzureMonitorTraceExporter reads APPLICATIONINSIGHTS_CONNECTION_STRING from
// environment or ApplicationInsights:ConnectionString from configuration.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ShelfLife.OverdueWorker"))
    .WithTracing(t => t
        .AddSource(OverdueReminderWorker.ActivitySource.Name)
        .AddAzureMonitorTraceExporter());

// ── Service Bus (needed by IMessagePublisher / OverdueReminderWorker) ─────────
builder.Services.AddSingleton(sp => new ServiceBusClient(
    builder.Configuration["ServiceBus:FullyQualifiedNamespace"],
    new DefaultAzureCredential()));
builder.Services.AddScoped<IMessagePublisher, ServiceBusPublisher>();

// ── Modules ───────────────────────────────────────────────────────────────────
builder.Services.AddLendingModule(builder.Configuration);

// Worker only uses LendingDbContext, so IUnitOfWork delegates straight to it.
builder.Services.AddScoped<ShelfLife.SharedKernel.IUnitOfWork>(sp =>
    (ShelfLife.SharedKernel.IUnitOfWork)sp.GetRequiredService<LendingDbContext>());

builder.Services.AddHostedService<OverdueReminderWorker>();

var host = builder.Build();
host.Run();
