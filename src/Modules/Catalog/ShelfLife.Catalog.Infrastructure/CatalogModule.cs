using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShelfLife.Catalog.Application;
using ShelfLife.Catalog.Domain;

namespace ShelfLife.Catalog.Infrastructure;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<CatalogDbContext>(opts =>
            opts.UseSqlServer(config.GetConnectionString("ShelfLife")));

        services.AddScoped<IBookTitleRepository, BookTitleRepository>();
        services.AddScoped<AddBookByIsbnHandler>();
        services.AddScoped<AddCopyHandler>();

        services.AddHttpClient<IIsbnEnrichmentService, IsbnEnrichmentService>()
            .AddStandardResilienceHandler();

        return services;
    }
}
