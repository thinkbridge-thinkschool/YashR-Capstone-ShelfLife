using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShelfLife.Catalog.Application;
using ShelfLife.Infrastructure.Messaging;
using Testcontainers.MsSql;

namespace ShelfLife.Api.IntegrationTests.Fixtures;

/// <summary>
/// Single factory shared across the entire "Integration" xUnit collection.
/// Starts one SQL Server Testcontainer, injects its connection string, and
/// replaces infrastructure stubs so tests never hit Azure or the internet.
/// </summary>
public sealed class ShelfLifeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    // Expose connection string for direct DB assertion in tests
    public string ConnectionString => _sql.GetConnectionString();

    public Task InitializeAsync() => _sql.StartAsync();

    public new async Task DisposeAsync()
    {
        await _sql.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Force Development so Program.cs uses HS256 JWT (no Entra dependency)
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Point all five DbContexts at the container database
                ["ConnectionStrings:ShelfLife"] = _sql.GetConnectionString(),

                // HS256 keys used by JwtService and test token helper
                ["Jwt:Issuer"] = TestTokenHelper.Issuer,
                ["Jwt:Audience"] = TestTokenHelper.Audience,
                ["Jwt:Secret"] = TestTokenHelper.Secret,

                // Prevent ServiceBusClient from throwing on null namespace
                ["ServiceBus:FullyQualifiedNamespace"] = "fake.servicebus.windows.net",

                // Skip Azure Monitor (no connection string → no-op in Program.cs)
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "",

                // Remove the rate-limit ceiling so all tests can register members freely
                ["RateLimiter:Identity:PermitLimit"] = "10000",
                ["RateLimiter:Api:PermitLimit"] = "10000",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace real Service Bus publisher with a no-op so tests never
            // try to authenticate against Azure
            services.RemoveAll<IMessagePublisher>();
            services.AddScoped<IMessagePublisher, NullMessagePublisher>();

            // Replace ISBN enrichment to avoid network calls to Open Library
            services.RemoveAll<IIsbnEnrichmentService>();
            services.AddScoped<IIsbnEnrichmentService, FakeIsbnEnrichmentService>();
        });
    }
}

/// <summary>xUnit collection definition — one container for all test classes.</summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<ShelfLifeApiFactory> { }
