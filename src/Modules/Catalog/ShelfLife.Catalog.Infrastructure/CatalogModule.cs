using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
            opts.UseSqlServer(config.GetConnectionString("ShelfLife"))
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IBookTitleRepository, BookTitleRepository>();
        services.AddScoped<AddBookByIsbnHandler>();
        services.AddScoped<AddBookManuallyHandler>();
        services.AddScoped<AddCopyHandler>();
        services.AddScoped<IBooksReadModel, BooksReadModel>();
        services.AddScoped<GetBooksHandler>();

        services.AddHttpClient<IIsbnEnrichmentService, IsbnEnrichmentService>()
            .AddStandardResilienceHandler();

        return services;
    }
}
