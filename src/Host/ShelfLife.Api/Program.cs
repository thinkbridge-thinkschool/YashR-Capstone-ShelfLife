using Azure.Identity;
using Azure.Messaging.ServiceBus;
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

// ── OpenTelemetry ─────────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ShelfLife.Api"))
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
