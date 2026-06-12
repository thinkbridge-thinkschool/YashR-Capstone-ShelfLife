using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ShelfLife.Api.Endpoints;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using ShelfLife.Catalog.Infrastructure;
using ShelfLife.Identity.Infrastructure;
using ShelfLife.Infrastructure.Messaging;
using ShelfLife.Infrastructure.Outbox;
using ShelfLife.Insights.Infrastructure;
using ShelfLife.Lending.Infrastructure;
using ShelfLife.Notifications.Infrastructure;
using System.Text;

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

// ── Auth ──────────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.MapInboundClaims = false;
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });
builder.Services.AddAuthorization();

// ── Service Bus ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton(new ServiceBusClient(
    builder.Configuration.GetConnectionString("ServiceBus")));
builder.Services.AddScoped<IMessagePublisher, ServiceBusPublisher>();

// ── Outbox relay ──────────────────────────────────────────────────────────────
builder.Services.AddHostedService<OutboxRelayWorker>();

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
