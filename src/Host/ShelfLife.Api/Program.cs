using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using ShelfLife.Api.Endpoints;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using ShelfLife.Catalog.Infrastructure;
using ShelfLife.Identity.Infrastructure;
using ShelfLife.Infrastructure.Messaging;
using ShelfLife.Insights.Infrastructure;
using ShelfLife.Lending.Infrastructure;
using ShelfLife.Notifications.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ───────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console()
       .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

// ── OpenTelemetry → Azure Monitor ─────────────────────────────────────────────
// UseAzureMonitor (on OpenTelemetryBuilder) reads APPLICATIONINSIGHTS_CONNECTION_STRING
// and auto-instruments AspNetCore, HttpClient, and SqlClient.
var otel = builder.Services.AddOpenTelemetry()
    .UseAzureMonitor()
    .WithTracing(t => t
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ShelfLife.Api"))
        .AddSource("Azure.Messaging.ServiceBus")); // capture Service Bus send spans

// In development, also dump traces to the console for quick local inspection.
if (builder.Environment.IsDevelopment())
    otel.WithTracing(t => t.AddConsoleExporter());

// ── Auth — Entra ID validates tokens using public JWKS keys; no client secret ──
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Librarian", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("roles", "Librarian"));
});

// ── Service Bus ───────────────────────────────────────────────────────────────
// Uses the namespace FQDN from config + DefaultAzureCredential (Managed Identity
// in Azure, developer credential locally). No connection string / SAS key.
builder.Services.AddSingleton(sp => new ServiceBusClient(
    builder.Configuration["ServiceBus:FullyQualifiedNamespace"],
    new DefaultAzureCredential()));
builder.Services.AddScoped<IMessagePublisher, ServiceBusPublisher>();

// ── Modules ───────────────────────────────────────────────────────────────────
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddLendingModule(builder.Configuration);
builder.Services.AddInsightsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

// ── Unit of Work — composite saves all module contexts in one call ────────────
builder.Services.AddScoped<ShelfLife.SharedKernel.IUnitOfWork>(sp =>
    new CompositeUnitOfWork(
        sp.GetRequiredService<IdentityDbContext>(),
        sp.GetRequiredService<CatalogDbContext>(),
        sp.GetRequiredService<LendingDbContext>(),
        sp.GetRequiredService<InsightsDbContext>(),
        sp.GetRequiredService<NotificationsDbContext>()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── DB: create schema on first run (EnsureCreated is idempotent) ──────────────
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var sp = scope.ServiceProvider;
    foreach (var ctx in new DbContext[]
    {
        sp.GetRequiredService<LendingDbContext>(),
        sp.GetRequiredService<CatalogDbContext>(),
        sp.GetRequiredService<IdentityDbContext>(),
        sp.GetRequiredService<InsightsDbContext>(),
        sp.GetRequiredService<NotificationsDbContext>(),
    })
        await ctx.Database.EnsureCreatedAsync();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

// ── Module endpoint registration ──────────────────────────────────────────────
app.MapGroup("/api/identity").MapIdentityEndpoints();
app.MapGroup("/api/catalog").MapCatalogEndpoints().RequireAuthorization();
app.MapGroup("/api/lending").MapLendingEndpoints().RequireAuthorization();
app.MapGroup("/api/insights").MapInsightsEndpoints().RequireAuthorization("Librarian");

app.Run();

// Needed for WebApplicationFactory in integration tests
public partial class Program { }

// Saves every module's DbContext so any handler's IUnitOfWork call persists its changes
// regardless of which context tracked the entity.
file sealed class CompositeUnitOfWork : ShelfLife.SharedKernel.IUnitOfWork
{
    private readonly Microsoft.EntityFrameworkCore.DbContext[] _contexts;
    public CompositeUnitOfWork(params Microsoft.EntityFrameworkCore.DbContext[] contexts) => _contexts = contexts;
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var total = 0;
        foreach (var ctx in _contexts)
            total += await ctx.SaveChangesAsync(ct);
        return total;
    }
}
