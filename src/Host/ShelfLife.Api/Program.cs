using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShelfLife.Api.Endpoints;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using ShelfLife.Catalog.Infrastructure;
using ShelfLife.Identity.Application;
using ShelfLife.Identity.Domain;
using ShelfLife.Identity.Infrastructure;
using ShelfLife.Infrastructure.Messaging;
using ShelfLife.Infrastructure.Outbox;
using ShelfLife.Infrastructure.Persistence;
using ShelfLife.Insights.Infrastructure;
using ShelfLife.Lending.Infrastructure;
using ShelfLife.Notifications.Infrastructure;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Kestrel hardening ─────────────────────────────────────────────────────────
// Remove the Server: Kestrel header — ZAP flags it as version fingerprinting.
// Cap request bodies at 64 KB — prevents memory-pressure DoS via huge JSON.
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = 65_536; // 64 KB
});

// ── Serilog ───────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console()
       .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

// ── OpenTelemetry → Azure Monitor ─────────────────────────────────────────────
// UseAzureMonitor requires APPLICATIONINSIGHTS_CONNECTION_STRING; skip it locally
// where the env var is absent (it only exists in Azure via Key Vault reference).
var otel = builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ShelfLife.Api"))
        .AddSource("Azure.Messaging.ServiceBus"));

var aiConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(aiConnectionString))
    otel.UseAzureMonitor();

if (builder.Environment.IsDevelopment())
    otel.WithTracing(t => t.AddConsoleExporter());

// ── Auth ──────────────────────────────────────────────────────────────────────
// Production: Entra ID validates tokens via JWKS (no secret in config).
// Development: local HS256 Bearer scheme so the stack runs without a real
//   Azure AD tenant. JwtService issues tokens with ClaimTypes.Role.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();

    // Read JWT config lazily via IConfiguration so WebApplicationFactory
    // in-memory overrides are present before the options are resolved.
    builder.Services
        .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IConfiguration>((opts, config) =>
        {
            // Keep claim names as-is (sub, email, etc.) so LendingEndpoints can
            // call user.FindFirstValue("sub") without hitting the default mapping
            // that would rename "sub" → ClaimTypes.NameIdentifier.
            opts.MapInboundClaims = false;
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer   = true,
                ValidIssuer      = config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience    = config["Jwt:Audience"],
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(config["Jwt:Secret"] ?? "")),
            };
        });
}
else
{
    builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Librarian", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("role", "Librarian"));
});

// ── Rate Limiting ─────────────────────────────────────────────────────────────
// "identity" — tight window on /register and /login to blunt credential stuffing
//              and mass account creation (S-01, S-02 from threat model).
// "api"      — general sliding window on authenticated endpoints (D-01).
// Partition key is the remote IP so each client gets its own bucket.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("identity", cfg =>
    {
        cfg.Window              = TimeSpan.FromMinutes(1);
        cfg.PermitLimit         = builder.Configuration.GetValue("RateLimiter:Identity:PermitLimit", 10);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit          = 0;  // reject immediately — no queue
    });

    options.AddFixedWindowLimiter("api", cfg =>
    {
        cfg.Window              = TimeSpan.FromMinutes(1);
        cfg.PermitLimit         = builder.Configuration.GetValue("RateLimiter:Api:PermitLimit", 60);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit          = 0;
    });

    // Return 429 with Retry-After header instead of the default 503
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── CORS ──────────────────────────────────────────────────────────────────────
// Allows the Angular dev server (port 4200) to reach the API during development.
// The policy is applied only when IsDevelopment() is true (see pipeline below).
builder.Services.AddCors(options =>
    options.AddPolicy("DevAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ── Service Bus ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton(sp => new ServiceBusClient(
    builder.Configuration["ServiceBus:FullyQualifiedNamespace"],
    new DefaultAzureCredential()));
builder.Services.AddScoped<IMessagePublisher, ServiceBusPublisher>();

// ── Outbox relay ──────────────────────────────────────────────────────────────
// EfOutboxStore uses IdentityDbContext because Identity's migration owns the
// OutboxMessages and DeadLetterMessages tables (no circular dependency).
builder.Services.AddScoped<IOutboxStore>(sp =>
    new EfOutboxStore(sp.GetRequiredService<IdentityDbContext>()));
builder.Services.AddScoped<OutboxRelayProcessor>();
builder.Services.AddHostedService<OutboxRelayWorker>();

// ── Modules ───────────────────────────────────────────────────────────────────
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddLendingModule(builder.Configuration);
builder.Services.AddInsightsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── Unit of Work ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ShelfLife.SharedKernel.IUnitOfWork>(sp =>
    new CompositeUnitOfWork(
        sp.GetRequiredService<IdentityDbContext>(),
        sp.GetRequiredService<CatalogDbContext>(),
        sp.GetRequiredService<LendingDbContext>(),
        sp.GetRequiredService<InsightsDbContext>(),
        sp.GetRequiredService<NotificationsDbContext>()));

builder.Services.AddEndpointsApiExplorer();

// ── OpenAPI / Swagger ─────────────────────────────────────────────────────────
// v1 doc with JWT Bearer security scheme so the Swagger UI shows a padlock
// on every authenticated endpoint and lets testers authorise in-browser.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "ShelfLife API",
        Version     = "v1",
        Description = "Library management system — borrowing, holds, catalog, insights."
    });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Paste your JWT (without the 'Bearer ' prefix). Obtain one from POST /api/v1/identity/login."
    };
    options.AddSecurityDefinition("Bearer", bearerScheme);

    // Apply the Bearer scheme globally so every operation shows the padlock
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── Security headers middleware ───────────────────────────────────────────────
// Applied to every response. Fixes ZAP medium-severity findings:
//   • Missing Anti-clickjacking Header
//   • X-Content-Type-Options Header Missing
//   • Referrer-Policy Header Not Set
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("X-Content-Type-Options",  "nosniff");
    ctx.Response.Headers.Append("X-Frame-Options",         "DENY");
    ctx.Response.Headers.Append("Referrer-Policy",         "strict-origin-when-cross-origin");
    ctx.Response.Headers.Append("Permissions-Policy",      "geolocation=(), camera=(), microphone=()");
    // Swagger UI needs inline scripts/styles; all other paths get the strict API policy.
    var csp = ctx.Request.Path.StartsWithSegments("/swagger")
        ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:"
        : "default-src 'none'; frame-ancestors 'none'";
    ctx.Response.Headers.Append("Content-Security-Policy", csp);
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // Swagger UI is served only in Development; production has no spec endpoint
    app.UseSwaggerUI(ui => ui.SwaggerEndpoint("/swagger/v1/swagger.json", "ShelfLife API v1"));
}
else
{
    // HSTS: tell browsers to enforce HTTPS for 1 year (supplements httpsOnly in Bicep)
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ── DB: apply EF Core migrations ─────────────────────────────────────────────
// MigrateAsync is idempotent — it consults __EFMigrationsHistory and only
// applies pending migrations, so subsequent restarts are safe.
// IdentityDbContext runs first because it owns the shared OutboxMessages table.
{
    await using var scope = app.Services.CreateAsyncScope();
    var sp   = scope.ServiceProvider;
    var log  = sp.GetRequiredService<ILogger<Program>>();
    var ctxs = new DbContext[]
    {
        sp.GetRequiredService<IdentityDbContext>(),
        sp.GetRequiredService<CatalogDbContext>(),
        sp.GetRequiredService<LendingDbContext>(),
        sp.GetRequiredService<InsightsDbContext>(),
        sp.GetRequiredService<NotificationsDbContext>(),
    };

    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            foreach (var ctx in ctxs)
                await ctx.Database.MigrateAsync();
            log.LogInformation("EF Core migrations applied successfully");
            break;
        }
        catch (Exception ex) when (ex.Message.Contains("15247") || ex.Message.Contains("does not have permission"))
        {
            // Managed identity lacks ALTER permission (SQL error 15247).
            // A DBA must run: ALTER ROLE db_ddladmin ADD MEMBER [shelflife-dev-api]
            log.LogError("DB migration skipped — managed identity lacks ALTER permission. " +
                "Grant db_ddladmin to the managed identity. Error: {Msg}", ex.Message);
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            log.LogWarning("DB not ready (attempt {Attempt}/10): {Message} — retrying in 3 s",
                attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "DB migration failed after 10 attempts — app starts degraded");
            break;
        }
    }
}

// ── Dev seed ──────────────────────────────────────────────────────────────────
// Creates a Librarian account for local demo if one does not already exist.
// Guarded by IsDevelopment() — never runs in staging or production.
if (app.Environment.IsDevelopment())
    await SeedLibrarianAsync(app.Services);

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
    app.UseCors("DevAngular");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── API v1 routes ─────────────────────────────────────────────────────────────
// URL-segment versioning: /api/v1/... makes the contract version explicit so
// future breaking changes ship under /api/v2/... without touching existing clients.
app.MapGroup("/api/v1/identity")
   .MapIdentityEndpoints()
   .RequireRateLimiting("identity");

app.MapGroup("/api/v1/catalog")
   .MapCatalogEndpoints()
   .RequireAuthorization()
   .RequireRateLimiting("api");

app.MapGroup("/api/v1/lending")
   .MapLendingEndpoints()
   .RequireAuthorization()
   .RequireRateLimiting("api");

app.MapGroup("/api/v1/insights")
   .MapInsightsEndpoints()
   .RequireAuthorization("Librarian")
   .RequireRateLimiting("api");

app.MapHealthChecks("/health");

app.Run();

static async Task SeedLibrarianAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var sp      = scope.ServiceProvider;
    var members = sp.GetRequiredService<IMemberRepository>();
    var hasher  = sp.GetRequiredService<IPasswordHasher>();
    var uow     = sp.GetRequiredService<ShelfLife.SharedKernel.IUnitOfWork>();

    const string email = "librarian@shelflife.dev";
    if (await members.FindByEmailAsync(email) is not null) return;

    var hash      = hasher.Hash("Librarian@123");
    var librarian = Member.Register(Guid.NewGuid(), email, "ShelfLife Librarian", hash);
    librarian.AssignRole(MemberRole.Librarian);
    await members.AddAsync(librarian);
    await uow.SaveChangesAsync();
}

// Needed for WebApplicationFactory in integration tests
public partial class Program { }

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
